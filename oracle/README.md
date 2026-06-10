# Oracle MDA SDK OTLP Demo

Standalone .NET 10 sample for Oracle Managed Data Access with OpenTelemetry SDK packages.

This demo intentionally does not use .NET auto-instrumentation. It exports application traces, metrics, and logs through OTLP to an OpenTelemetry Collector, then configures Oracle `DBMS_OBSERVABILITY` to point at the same collector so the collector output can be used to compare app-side spans with database server-side telemetry.

## Run with Docker Compose

```powershell
cd oracle
docker compose up --build --abort-on-container-exit
```

The compose stack starts:

- `oracle`: Oracle ADB Free, ATP workload, wallet generated into a shared volume.
- `otel-collector`: OTLP gRPC/HTTP receiver with detailed debug exporter.
- `app`: .NET 10 console app using OpenTelemetry SDK/exporter packages and `Oracle.ManagedDataAccess.Core`.

After a run, inspect collector output:

```powershell
docker compose logs otel-collector
```

Clean all containers and wallet state:

```powershell
docker compose down -v
```

## What to look for

The collector should receive telemetry from `service.name=oracle-mda-sdk-demo`:

- Manual app ActivitySource spans from `OracleMdaSdkDemo`.
- ODP.NET client spans from `Oracle.ManagedDataAccess.Core`, when emitted by the driver.
- App metrics and logs.

The app also runs:

```sql
dbms_observability.enable_service;
dbms_observability.enable_service_option(option_id => dbms_observability.capture_traces);
dbms_observability.add_endpoint(... otel_traces ...);
dbms_observability.enable_endpoint(...);
```

The intended comparison is whether any database server-side spans appear in collector output after that setup. If only the app/ODP.NET spans appear, the sample demonstrates the current lack of server-side spans reaching the collector.

## Current validation result

In this environment, span context propagation from the application to the Oracle server is working.

The evidence is the Oracle diagnostic trace archive, not a server-side OTLP span. For a post-configuration query, the collector reported an ODP.NET client span:

- Scope: `Oracle.ManagedDataAccess.Core 23.26.200`
- Span name: `SendExecuteRequest`
- Trace ID: `9a2a7deedd2b576083e78923a00b6dc5`
- Span ID: `d00a2b6269bf0fdb`

Oracle archived the same trace ID and span ID as the parent ID in its diagnostic trace files:

```text
/u01/app/oracle/diag/rdbms/pod1/POD1/trace/POD1_dt01_678.trc:43:
KSTRC: Operation [traceid-9a2a7deedd2b576083e78923a00b6dc5:parentid-d00a2b6269bf0fdb] ... archived to: ksu_ops_POD1.trc
```

That confirms that the trace context reached the Oracle server and was associated with server-side work.

However, no separate Oracle server-side OTLP trace spans were observed on the collector in this setup. The collector received the app/ODP.NET spans from `service.name=oracle-mda-sdk-demo`, but after waiting for delayed export, there was no additional trace batch attributable to the Oracle server. `DBMS_OBSERVABILITY` reported tracing and the OTLP endpoint as enabled:

```text
Service "Traces": Enabled
Option "Capture traces": Enabled
Endpoint "http://otel-collector:4318/v1/traces": Enabled
```

The exposed `DBMS_OBSERVABILITY` package surface did not show a flush/export procedure that could force queued spans to be sent immediately. So the current conclusion is:

- Propagation to the Oracle server: validated through Oracle diagnostic trace archive.
- Server-side OTLP span export to the collector: not observed in this demo run.

## Local run

The project is rooted in this folder. These commands should work from `oracle/`:

```powershell
dotnet restore
dotnet build
dotnet run
```

For a local run outside compose, set:

- `OTEL_EXPORTER_OTLP_ENDPOINT`
- `ORACLE_USER`
- `ORACLE_PASSWORD`
- `ORACLE_DATA_SOURCE`
- `ORACLE_WALLET_SOURCE`
- `ORACLE_CONNECT_HOST`
- `ORACLE_DB_OTLP_TRACES_ENDPOINT`

The app copies the wallet to a temporary writable folder and rewrites `tnsnames.ora` to use `ORACLE_CONNECT_HOST` and `ORACLE_CONNECT_PORT`.
