# Oracle MDA SDK OTLP Demo

Standalone .NET 10 sample for Oracle Managed Data Access with OpenTelemetry SDK packages.

This demo intentionally does not use .NET auto-instrumentation. It exports application traces, metrics, and logs through OTLP to an OpenTelemetry Collector, then configures Oracle `DBMS_OBSERVABILITY` to point at the same collector so the collector output can be used to compare app-side spans with database server-side telemetry.

## Run with Docker Compose

```powershell
cd oracle
docker compose up --build --abort-on-container-exit
```

The compose stack starts:

- `oracle`: Oracle ADB Free, ATP workload, wallet generated into a shared volume. The image is built locally from `OracleServer.Dockerfile`.
- `otel-collector`: OTLP gRPC receiver and OTLP/HTTP HTTPS receiver with detailed debug exporter.
- `app`: .NET 10 console app using OpenTelemetry SDK/exporter packages and `Oracle.ManagedDataAccess.Core`.

## Demo TLS certificates

Oracle server-side OTLP/HTTP export is configured to use HTTPS:

```text
https://otel-collector:4318/v1/traces
```

The collector's OTLP/HTTP receiver on port `4318` therefore uses TLS. The demo uses a local, non-production certificate chain:

- `certs/otel-demo-root-ca.crt`: local demo root CA.
- `certs/otel-demo-root-ca.crl`: local demo root CA certificate revocation list.
- `certs/otel-collector.crt`: collector leaf certificate signed by that root CA. It has SANs for `otel-collector`, `localhost`, and `127.0.0.1`, plus CRL Distribution Points for Docker-network and host-local validation.
- `certs/otel-collector.key`: collector leaf private key.

`otel-collector.yaml` mounts the leaf certificate and private key into the collector. `docker-compose.yml` also starts `otel-crl`, a small HTTP server that publishes `otel-demo-root-ca.crl` at both certificate CRL Distribution Point addresses:

- `http://otel-crl:8080/otel-demo-root-ca.crl` from Docker containers, including Oracle.
- `http://localhost:8080/otel-demo-root-ca.crl` from the host.

`OracleServer.Dockerfile` builds a local Oracle image from Oracle ADB Free `26.2.4.2-26ai` and installs `otel-demo-root-ca.crt` into Oracle Linux trust with `update-ca-trust extract`.

This is diagnostic-only. Do not reuse these certificates or private keys outside local testing. For a real deployment, use a certificate issued by the environment's trusted CA and configure Oracle trust through the supported Oracle path for that environment.

You can verify from inside the Oracle container that the collector certificate chains to the local root CA:

```powershell
docker compose exec oracle bash -lc "openssl s_client -connect otel-collector:4318 -servername otel-collector -verify_return_error </dev/null 2>&1 | grep -E 'subject=|issuer=|Verify return code'"
```

Expected result:

```text
subject=CN = otel-collector
issuer=CN = otel-demo-root-ca
Verify return code: 0 (ok)
```

You can also verify that the CRL is reachable and usable from inside the Oracle container:

```bash
docker compose exec oracle bash -lc "
  openssl s_client -showcerts -connect otel-collector:4318 -servername otel-collector -verify_return_error </dev/null 2>/tmp/otel-sclient.err |
    sed -n '/BEGIN CERTIFICATE/,/END CERTIFICATE/p' > /tmp/otel-collector-from-server.crt
  curl -fsS http://otel-crl:8080/otel-demo-root-ca.crl -o /tmp/otel-demo-root-ca.crl
  openssl verify -crl_check \
    -CAfile /etc/pki/ca-trust/source/anchors/otel-demo-root-ca.crt \
    -CRLfile /tmp/otel-demo-root-ca.crl \
    /tmp/otel-collector-from-server.crt
"
```

Expected result:

```text
/tmp/otel-collector-from-server.crt: OK
```

From the Windows host, this should now validate without `--ssl-no-revoke` because the certificate includes a host-local CRL Distribution Point and the CRL service exposes it on port `8080`:

```powershell
curl.exe --cacert .\certs\otel-demo-root-ca.crt https://localhost:4318/v1/traces
```

Expected result is HTTP `405 Method Not Allowed`, because a browser or `curl` GET is not a valid OTLP/HTTP trace POST. The important part is that the TLS handshake no longer fails with an unknown revocation status.

To regenerate the local diagnostic certificates and CRL, run from `oracle/` through an image that has OpenSSL:

```powershell
docker run --rm -v "${PWD}:/work" -w /work mcr.microsoft.com/dotnet/runtime:10.0 bash ./generate-demo-certs.sh
```

Only the `.crt` root, `.crl` revocation list, `.crt` leaf, and `.key` leaf are needed by compose. The generated root private key, CSR, and CA database files are local regeneration artifacts and should not be committed or shared.

If the collector fails with `permission denied` while opening `/etc/otelcol-contrib/certs/otel-collector.key`, make the generated key readable by the non-root collector process:

```powershell
docker run --rm -v "${PWD}\certs:/certs" mcr.microsoft.com/dotnet/runtime:10.0 bash -lc "chmod 0644 /certs/otel-collector.key /certs/otel-collector.crt"
```

After a run, inspect collector output:

```powershell
docker compose logs otel-collector
```

Clean all containers and wallet state:

```powershell
docker compose down -v
```

## Oracle image note

The newer `ghcr.io/oracle/adb-free:26.5.4.2-26ai` image was tested. It reports Oracle database version `23.26.2.1.0`, but in this environment `DBMS_OBSERVABILITY.show_service_status(dbms_observability.all_info)` showed `Traces` and `Logs` enabled at the container level while runtime remained disabled, even after `dbms_observability.enable_service` and a container restart.

This demo therefore uses the older `26.2.4.2-26ai` base image because it reports the expected runtime state:

```json
{
  "Runtime": [
    { "Service": "Traces", "Enabled": 1 },
    { "Service": "Logs", "Enabled": 1 },
    { "Option": "Capture traces", "Enabled": 1 }
  ]
}
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
- Trace ID: `f856138e43c6f9a29793fe67d103d7b9`
- Span ID: `ca03e2b341937498`

Oracle archived the same trace ID and span ID as the parent ID in its diagnostic trace files:

```text
/u01/app/oracle/diag/rdbms/pod1/POD1/trace/POD1_dt01_647.trc:43:
KSTRC: Operation [traceid-f856138e43c6f9a29793fe67d103d7b9:parentid-ca03e2b341937498] ... archived to: ksu_ops_POD1.trc
```

That confirms that the trace context reached the Oracle server and was associated with server-side work.

However, no separate Oracle server-side OTLP trace spans were observed on the collector in this setup. The collector received the app/ODP.NET spans from `service.name=oracle-mda-sdk-demo`, but after waiting for delayed export, there was no additional trace batch attributable to the Oracle server. `DBMS_OBSERVABILITY` reported tracing and the OTLP endpoint as enabled:

```text
Service "Traces": Enabled
Option "Capture traces": Enabled
Endpoint "https://otel-collector:4318/v1/traces": Enabled
```

The exposed `DBMS_OBSERVABILITY` package surface did not show a flush/export procedure that could force queued spans to be sent immediately. So the current conclusion is:

- Propagation to the Oracle server: validated through Oracle diagnostic trace archive.
- Server-side OTLP span export to the collector: not observed in this demo run.

The 1000-iteration DML workload can be used to generate more database work:

```powershell
docker compose run --rm --no-deps -e WORKLOAD=dml -e ITERATIONS=1000 -e DELAY_SECONDS=0 -e FLUSH_SECONDS=60 -e LOG_EVERY=100 app
```

In the latest local run, the collector received 5010 app/ODP.NET spans across these scopes only:

```text
InstrumentationScope Oracle.ManagedDataAccess.Core 23.26.200
InstrumentationScope OracleMdaSdkDemo 1.0.0
```

The Oracle diagnostic archive contained 2008 `KSTRC` archived operations, confirming propagated context at volume, but no separate Oracle server-side OTLP spans appeared in collector output.

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
