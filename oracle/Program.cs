using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Oracle.ManagedDataAccess.Client;

const string ServiceName = "oracle-mda-sdk-demo";
const string ActivitySourceName = "OracleMdaSdkDemo";
const string MeterName = "OracleMdaSdkDemo";
const string OracleMdaActivitySourceName = "Oracle.ManagedDataAccess.Core";

var settings = DemoSettings.FromEnvironment();
var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(ServiceName, serviceVersion: "1.0.0")
    .AddAttributes(
    [
        new KeyValuePair<string, object>("demo.oracle.data_source", settings.OracleDataSource),
        new KeyValuePair<string, object>("demo.oracle.configure_db_observability", settings.ConfigureDatabaseObservability),
        new KeyValuePair<string, object>("demo.oracle.database_open_telemetry_tracing", settings.DatabaseOpenTelemetryTracing)
    ]);

using var activitySource = new ActivitySource(ActivitySourceName, "1.0.0");
using var meter = new Meter(MeterName, "1.0.0");
var queryCounter = meter.CreateCounter<long>("oracle.demo.queries");
var queryDuration = meter.CreateHistogram<double>("oracle.demo.query.duration.ms", unit: "ms");

using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });

    logging.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;
        options.SetResourceBuilder(resourceBuilder);
        options.AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = settings.OtlpEndpoint;
            otlp.Protocol = OtlpExportProtocol.Grpc;
        });
    });
});

var logger = loggerFactory.CreateLogger(ServiceName);

OracleConfiguration.DatabaseOpenTelemetryTracing = settings.DatabaseOpenTelemetryTracing;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(ActivitySourceName)
    .AddSource(OracleMdaActivitySourceName)
    .AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = settings.OtlpEndpoint;
        otlp.Protocol = OtlpExportProtocol.Grpc;
    })
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(MeterName)
    .AddRuntimeInstrumentation()
    .AddProcessInstrumentation()
    .AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = settings.OtlpEndpoint;
        otlp.Protocol = OtlpExportProtocol.Grpc;
    })
    .Build();

logger.LogInformation("Starting Oracle MDA SDK demo. OTLP endpoint: {Endpoint}", settings.OtlpEndpoint);
logger.LogInformation("OracleConfiguration.DatabaseOpenTelemetryTracing={DatabaseOpenTelemetryTracing}", OracleConfiguration.DatabaseOpenTelemetryTracing);

var tnsAdmin = await WalletPreparer.PrepareAsync(settings, logger).ConfigureAwait(false);
if (!string.IsNullOrWhiteSpace(tnsAdmin))
{
    Environment.SetEnvironmentVariable("TNS_ADMIN", tnsAdmin);
    logger.LogInformation("Using TNS_ADMIN={TnsAdmin}", tnsAdmin);
}

var connectionString = settings.CreateConnectionString();
await WaitForOracleAsync(connectionString, settings, logger).ConfigureAwait(false);

if (settings.ConfigureDatabaseObservability)
{
    await TryConfigureDatabaseObservabilityAsync(connectionString, settings, logger).ConfigureAwait(false);
}

for (var iteration = 1; iteration <= settings.Iterations; iteration++)
{
    await RunQueryAsync(connectionString, settings, iteration, activitySource, queryCounter, queryDuration, logger).ConfigureAwait(false);
    await Task.Delay(settings.DelayBetweenQueries).ConfigureAwait(false);
}

logger.LogInformation(
    "Finished app-side work. Watch collector output for service.name={ServiceName}. Database server-side spans may be absent even after DBMS_OBSERVABILITY endpoint setup.",
    ServiceName);

await Task.Delay(settings.FlushDelay).ConfigureAwait(false);
tracerProvider.ForceFlush();
meterProvider.ForceFlush();

static async Task WaitForOracleAsync(string connectionString, DemoSettings settings, ILogger logger)
{
    var deadline = DateTimeOffset.UtcNow.Add(settings.OracleStartupTimeout);
    var attempt = 0;

    while (DateTimeOffset.UtcNow < deadline)
    {
        attempt++;

        try
        {
            await using var connection = CreateOracleConnection(connectionString, settings);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "select 1 from dual";
            _ = await command.ExecuteScalarAsync().ConfigureAwait(false);

            logger.LogInformation("Oracle connection succeeded after {Attempt} attempt(s).", attempt);
            return;
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException)
        {
            logger.LogInformation(ex, "Oracle is not ready yet. Attempt {Attempt}.", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    throw new TimeoutException($"Oracle was not ready after {settings.OracleStartupTimeout}.");
}

static async Task TryConfigureDatabaseObservabilityAsync(string connectionString, DemoSettings settings, ILogger logger)
{
    var endpoint = EscapeSqlLiteral(settings.DatabaseOtlpTracesEndpoint.ToString());
    var sql = @"
begin
  dbms_observability.enable_service;
  dbms_observability.enable_service_option(option_id => dbms_observability.capture_traces);
  dbms_observability.add_endpoint(
    endpoint_type => dbms_observability.otel_traces,
    endpoint => '" + endpoint + @"',
    credential_name => NULL);
  dbms_observability.enable_endpoint(endpoint => '" + endpoint + @"');
end;";

    try
    {
        await using var connection = CreateOracleConnection(connectionString, settings);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        logger.LogInformation("Configured DBMS_OBSERVABILITY endpoint: {Endpoint}", settings.DatabaseOtlpTracesEndpoint);
    }
    catch (OracleException ex)
    {
        logger.LogWarning(
            ex,
            "Could not configure DBMS_OBSERVABILITY endpoint. The app will continue so client-side SDK telemetry can still be observed.");
    }
}

static async Task RunQueryAsync(
    string connectionString,
    DemoSettings settings,
    int iteration,
    ActivitySource activitySource,
    Counter<long> queryCounter,
    Histogram<double> queryDuration,
    ILogger logger)
{
    using var activity = activitySource.StartActivity("oracle.demo.select", ActivityKind.Client);
    activity?.SetTag("db.system", "oracle");
    activity?.SetTag("db.operation.name", "SELECT");
    activity?.SetTag("db.query.text", "select sys_context('USERENV','SERVICE_NAME'), systimestamp from dual");
    activity?.SetTag("demo.iteration", iteration);

    var stopwatch = Stopwatch.StartNew();

    await using var connection = CreateOracleConnection(connectionString, settings);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "select sys_context('USERENV','SERVICE_NAME') as service_name, systimestamp as server_time from dual";

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        var serviceName = reader.GetString(0);
        var serverTime = reader.GetValue(1);
        logger.LogInformation(
            "Oracle query {Iteration} returned service={ServiceName}, server_time={ServerTime}",
            iteration,
            serviceName,
            serverTime);
    }

    stopwatch.Stop();
    queryCounter.Add(1, new KeyValuePair<string, object?>("db.system", "oracle"));
    queryDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("db.system", "oracle"));
}

static string EscapeSqlLiteral(string value)
{
    return value.Replace("'", "''", StringComparison.Ordinal);
}

static OracleConnection CreateOracleConnection(string connectionString, DemoSettings settings)
{
    var connection = new OracleConnection();
    connection.DatabaseOpenTelemetryTracing = settings.DatabaseOpenTelemetryTracing;
    connection.SSLServerDNMatch = false;

    connection.ConnectionString = connectionString;
    return connection;
}

file sealed record DemoSettings(
    Uri OtlpEndpoint,
    string OracleUser,
    string OraclePassword,
    string OracleDataSource,
    string OracleWalletSource,
    string OracleWalletWorkDirectory,
    string OracleConnectHost,
    int OracleConnectPort,
    bool ConfigureDatabaseObservability,
    bool DatabaseOpenTelemetryTracing,
    Uri DatabaseOtlpTracesEndpoint,
    int Iterations,
    TimeSpan DelayBetweenQueries,
    TimeSpan FlushDelay,
    TimeSpan OracleStartupTimeout)
{
    public static DemoSettings FromEnvironment()
    {
        return new DemoSettings(
            OtlpEndpoint: ReadUri("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317"),
            OracleUser: ReadString("ORACLE_USER", "ADMIN"),
            OraclePassword: ReadString("ORACLE_PASSWORD", "Otel1ADebugPassword"),
            OracleDataSource: ReadString("ORACLE_DATA_SOURCE", "myatp_low"),
            OracleWalletSource: ReadString("ORACLE_WALLET_SOURCE", "/oracle-wallets/tls_wallet"),
            OracleWalletWorkDirectory: ReadString("ORACLE_WALLET_WORKDIR", Path.Combine(Path.GetTempPath(), "oracle-wallet")),
            OracleConnectHost: ReadString("ORACLE_CONNECT_HOST", "oracle"),
            OracleConnectPort: ReadInt("ORACLE_CONNECT_PORT", 1522),
            ConfigureDatabaseObservability: ReadBool("CONFIGURE_DB_OBSERVABILITY", true),
            DatabaseOpenTelemetryTracing: ReadBool("ORACLE_DATABASE_OPEN_TELEMETRY_TRACING", true),
            DatabaseOtlpTracesEndpoint: ReadUri("ORACLE_DB_OTLP_TRACES_ENDPOINT", "http://otel-collector:4318/v1/traces"),
            Iterations: ReadInt("ITERATIONS", 3),
            DelayBetweenQueries: TimeSpan.FromSeconds(ReadInt("DELAY_SECONDS", 2)),
            FlushDelay: TimeSpan.FromSeconds(ReadInt("FLUSH_SECONDS", 10)),
            OracleStartupTimeout: TimeSpan.FromMinutes(ReadInt("ORACLE_STARTUP_TIMEOUT_MINUTES", 10)));
    }

    public string CreateConnectionString()
    {
        return $"User Id={OracleUser};Password={OraclePassword};Data Source={OracleDataSource};";
    }

    private static string ReadString(string name, string defaultValue)
    {
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : defaultValue;
    }

    private static int ReadInt(string name, int defaultValue)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;
    }

    private static bool ReadBool(string name, bool defaultValue)
    {
        return bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;
    }

    private static Uri ReadUri(string name, string defaultValue)
    {
        return new Uri(ReadString(name, defaultValue), UriKind.Absolute);
    }
}

file static class WalletPreparer
{
    public static async Task<string?> PrepareAsync(DemoSettings settings, ILogger logger)
    {
        if (!Directory.Exists(settings.OracleWalletSource))
        {
            logger.LogWarning(
                "Oracle wallet source {WalletSource} does not exist. The app will try to connect without setting TNS_ADMIN.",
                settings.OracleWalletSource);
            return null;
        }

        await WaitForWalletAsync(settings, logger).ConfigureAwait(false);

        if (Directory.Exists(settings.OracleWalletWorkDirectory))
        {
            Directory.Delete(settings.OracleWalletWorkDirectory, recursive: true);
        }

        Directory.CreateDirectory(settings.OracleWalletWorkDirectory);
        await CopyDirectoryAsync(settings.OracleWalletSource, settings.OracleWalletWorkDirectory).ConfigureAwait(false);
        RewriteWalletNetworkConfiguration(settings);
        return settings.OracleWalletWorkDirectory;
    }

    private static async Task WaitForWalletAsync(DemoSettings settings, ILogger logger)
    {
        var tnsNamesPath = Path.Combine(settings.OracleWalletSource, "tnsnames.ora");
        var deadline = DateTimeOffset.UtcNow.Add(settings.OracleStartupTimeout);
        var attempt = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            attempt++;

            if (File.Exists(tnsNamesPath))
            {
                var tnsNames = await File.ReadAllTextAsync(tnsNamesPath).ConfigureAwait(false);
                var dataSourceIsReady = !LooksLikeTnsAlias(settings.OracleDataSource) || ContainsTnsAlias(tnsNames, settings.OracleDataSource);
                if (dataSourceIsReady && AreWalletArtifactsReady(settings.OracleWalletSource))
                {
                    logger.LogInformation("Oracle wallet is ready after {Attempt} attempt(s).", attempt);
                    return;
                }
            }

            logger.LogInformation(
                "Oracle wallet is not ready yet. Attempt {Attempt}. Waiting for alias {DataSource} and wallet artifacts in {WalletSource}.",
                attempt,
                settings.OracleDataSource,
                settings.OracleWalletSource);
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Oracle wallet was not ready after {settings.OracleStartupTimeout}. Expected alias '{settings.OracleDataSource}' in '{tnsNamesPath}'.");
    }

    private static bool AreWalletArtifactsReady(string walletDirectory)
    {
        return HasMinimumSize("adb_container.cert", 512)
            && HasMinimumSize("cwallet.sso", 1024)
            && HasMinimumSize("ewallet.p12", 1024)
            && HasMinimumSize("keystore.jks", 1024)
            && HasMinimumSize("truststore.jks", 1024);

        bool HasMinimumSize(string fileName, long minimumBytes)
        {
            var path = Path.Combine(walletDirectory, fileName);
            return File.Exists(path) && new FileInfo(path).Length >= minimumBytes;
        }
    }

    private static async Task CopyDirectoryAsync(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            await using var input = File.OpenRead(file);
            await using var output = File.Create(target);
            await input.CopyToAsync(output).ConfigureAwait(false);
        }
    }

    private static void RewriteWalletNetworkConfiguration(DemoSettings settings)
    {
        var tnsNamesPath = Path.Combine(settings.OracleWalletWorkDirectory, "tnsnames.ora");
        var tnsNames = File.ReadAllText(tnsNamesPath);
        tnsNames = Regex.Replace(tnsNames, @"\(host\s*=\s*[^)]+\)", $"(host={settings.OracleConnectHost})", RegexOptions.IgnoreCase);
        tnsNames = Regex.Replace(tnsNames, @"\(port\s*=\s*\d+\)", $"(port={settings.OracleConnectPort})", RegexOptions.IgnoreCase);
        File.WriteAllText(tnsNamesPath, tnsNames);

        var sqlNet = $@"WALLET_LOCATION =
  (SOURCE =
    (METHOD = FILE)
    (METHOD_DATA =
      (DIRECTORY = {settings.OracleWalletWorkDirectory})
    )
  )
SSL_SERVER_DN_MATCH = no
";
        File.WriteAllText(Path.Combine(settings.OracleWalletWorkDirectory, "sqlnet.ora"), sqlNet);
    }

    private static bool LooksLikeTnsAlias(string dataSource)
    {
        return Regex.IsMatch(dataSource, @"^[A-Za-z][A-Za-z0-9_.-]*$");
    }

    private static bool ContainsTnsAlias(string tnsNames, string alias)
    {
        return Regex.IsMatch(tnsNames, $@"(?im)^\s*{Regex.Escape(alias)}\s*=");
    }
}
