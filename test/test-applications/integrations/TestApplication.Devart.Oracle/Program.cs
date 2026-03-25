// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Devart.Data.Oracle;
using TestApplication.Shared;

ConsoleHelper.WriteSplashScreen(args);

var oraclePort = GetOraclePort(args);
var oraclePassword = GetOraclePassword(args);

const string licenseKey =
    "PUT LICENSE HERE";

// The license key is only compatible with Devart 11.x+. Older versions use a different key format
// and throw LicenseException when the 11.x+ key is provided.
var devartVersion = typeof(OracleConnection).Assembly.GetName().Version;
var licenseKeyParam = devartVersion?.Major >= 11 ? $";License Key={licenseKey}" : string.Empty;
using var connection = new OracleConnection($"User Id=appuser;Password={oraclePassword};Direct=true;Server=localhost;Port={oraclePort};Service Name=XEPDB1{licenseKeyParam}");

await connection.OpenAsync().ConfigureAwait(false);

using (var command = new OracleCommand("CREATE TABLE otel_test (id NUMBER, value VARCHAR2(100))", connection))
{
    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
}

#pragma warning disable CA1849 // Intentionally calling both sync and async variants to ensure both are properly traced.

// Sync: ExecuteNonQuery
using (var command = new OracleCommand("INSERT INTO otel_test VALUES (1, 'value1')", connection))
{
    var affected = command.ExecuteNonQuery();
    Console.WriteLine($"ExecuteNonQuery affected rows: {affected}");
}

// Sync: ExecuteScalar
using (var command = new OracleCommand("SELECT COUNT(*) FROM otel_test", connection))
{
    var count = command.ExecuteScalar();
    Console.WriteLine($"ExecuteScalar result: {count}");
}

// Sync: ExecuteReader
using (var command = new OracleCommand("SELECT * FROM otel_test", connection))
{
    using var reader = command.ExecuteReader();
    Console.WriteLine($"ExecuteReader HasRows: {reader.HasRows}");
}

#pragma warning restore CA1849

// Async: ExecuteNonQueryAsync
using (var command = new OracleCommand("INSERT INTO otel_test VALUES (2, 'value2')", connection))
{
    var affected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    Console.WriteLine($"ExecuteNonQueryAsync affected rows: {affected}");
}

// Async: ExecuteScalarAsync
using (var command = new OracleCommand("SELECT COUNT(*) FROM otel_test", connection))
{
    var count = await command.ExecuteScalarAsync().ConfigureAwait(false);
    Console.WriteLine($"ExecuteScalarAsync result: {count}");
}

// Async: ExecuteReaderAsync
using (var command = new OracleCommand("SELECT * FROM otel_test", connection))
{
    using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    Console.WriteLine($"ExecuteReaderAsync HasRows: {reader.HasRows}");
}

using (var command = new OracleCommand("DROP TABLE otel_test", connection))
{
    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
}

static string GetOraclePort(string[] args)
{
    if (args.Length > 1)
    {
        return args[1];
    }

    return "1521";
}

static string GetOraclePassword(string[] args)
{
    if (args.Length > 3)
    {
        return args[3];
    }

    throw new NotSupportedException("Lack of password for the Oracle.");
}
