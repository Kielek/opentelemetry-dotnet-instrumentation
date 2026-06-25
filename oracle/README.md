# Oracle MDA SDK OTLP Demo

Standalone .NET 10 sample for Oracle Managed Data Access with OpenTelemetry SDK packages.

This demo intentionally does not use .NET auto-instrumentation. It exports application traces, metrics, and logs through OTLP to an OpenTelemetry Collector, then configures Oracle `DBMS_OBSERVABILITY` to point at the same collector so the collector output can be used to compare app-side spans with database server-side telemetry.

## Run with Docker Compose

```powershell
cd oracle
docker compose up --build --abort-on-container-exit
```

The compose stack starts:

- `oracle`: Oracle ADB Free `26.5.4.2-26ai`, ATP workload, wallet generated into a shared volume. The image is built locally from `OracleServer.Dockerfile`.
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

`OracleServer.Dockerfile` builds a local Oracle image from Oracle ADB Free `26.5.4.2-26ai` and installs `otel-demo-root-ca.crt` into Oracle Linux trust with `update-ca-trust extract`.

Oracle server-side OTLP export does not rely on the Oracle Linux system trust bundle in this image. Per Oracle DB team feedback for DB `23.26.2`, the collector certificate must also be trusted from the database observability wallet under:

```text
$WALLET_ROOT/<PDB_GUID>/disttrc
```

ADB Free defines `WALLET_ROOT` by default as `/u01/app/oracle/wallets`. The compose healthcheck runs `configure-observability-wallet.sh` after the PDB is reachable. That script resolves the active PDB GUID, preserves the existing wallet directory casing, creates the `disttrc` auto-login wallet if needed, and adds both `otel-demo-root-ca.crt` and `otel-collector.crt` with `orapki wallet add -trusted_cert`.

The `disttrc` wallet name is a `23.26.2` workaround. Oracle has indicated that this wallet name changes in `23.26.3`, so this script should be revisited when updating the database image.

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

You can inspect the database observability wallet that the server-side exporter uses:

```powershell
docker compose exec oracle bash -lc "cat /tmp/configure-observability-wallet.log"
```

Expected result includes the active PDB-specific `disttrc` wallet and both trusted certificates. The PDB GUID path is case-sensitive on Linux and must match the directory that already exists under `WALLET_ROOT`:

```text
/u01/app/oracle/wallets/<PDB_GUID>/disttrc
Trusted Certificates:
Subject: CN=otel-collector
Subject: CN=otel-demo-root-ca
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

The demo currently uses `ghcr.io/oracle/adb-free:26.5.4.2-26ai`, pinned by digest in `OracleServer.Dockerfile`. This image reports Oracle database version `23.26.2.1.0`.

This newer image is useful for reproducing the server-side export issue. In this environment `DBMS_OBSERVABILITY.show_service_status(dbms_observability.all_info)` fails because the `all_info` constant is not available:

```text
ORA-06553: PLS-221: 'ALL_INFO' is not a procedure or is undefined
```

Use numeric status selector `0` instead:

```sql
select dbms_observability.show_service_status(0) from dual;
```

After a clean rebuild and a successful app run, the newer image still reports runtime traces and logs disabled while the container-level service state is enabled:

```json
{
  "Runtime": [
    { "Service": "Traces", "Enabled": 0 },
    { "Service": "Logs", "Enabled": 0 },
    { "Option": "Capture traces", "Enabled": 1 }
  ],
  "Container": [
    { "Service": "Traces", "Enabled": 1 },
    { "Service": "Logs", "Enabled": 1 },
    { "Option": "Capture traces", "Enabled": 1 }
  ]
}
```

The older `26.2.4.2-26ai` image was previously tested as a comparison point and reported the expected runtime state:

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

The collector should also receive database server-side spans from `service.name=oracle-db`. In the validated local run, the Oracle server span had an empty instrumentation scope name/version, span name `DB Server`, and span kind `Server`.

The app also runs:

```sql
dbms_network_acl_admin.append_host_ace(... connect privilege for otel-collector:4318 ...);
dbms_network_acl_admin.append_host_ace(... resolve privilege for otel-collector ...);
dbms_observability.enable_service;
dbms_observability.enable_service_option(option_id => dbms_observability.capture_traces);
dbms_observability.add_endpoint(... otel_traces ...);
dbms_observability.enable_endpoint(...);
```

The local Oracle image also starts through `entrypoint-with-observability-wallet.sh`, which sets `_kstrc_service_mask=0` before the normal ADB entrypoint. This is diagnostic-only and was added because the local package comments indicate the service status can change only when the instance starts with KSTRC enabled.

The intended comparison is whether database server-side spans appear in collector output after that setup and share the trace with the ODP.NET round-trip span.

## Current validation result

In this environment, span context propagation from the application to the Oracle server is working and Oracle server-side spans are exported to the collector.

After applying the Oracle DB team workaround, a run verified:

- `$WALLET_ROOT/<PDB_GUID>/disttrc` exists at the existing uppercase PDB GUID path and contains trusted certs for `CN=otel-collector` and `CN=otel-demo-root-ca`.
- The database network ACL grants the configured user `connect` to `otel-collector:4318` and `resolve` for `otel-collector`.
- `DBMS_OBSERVABILITY` reports the OTLP traces endpoint `https://otel-collector:4318/v1/traces` as enabled.
- The collector receives both ODP.NET/app spans and an Oracle DB server-side span.

The Oracle DB server span batch observed in collector output:

```text
Resource attributes:
     -> service.name: Str(oracle-db)
InstrumentationScope
Span #0
    Trace ID       : aff62c6fc94449d1d768ff83593476b7
    Parent ID      : 632cea6d01e1fcb0
    ID             : 4d8a00d02f47160d
    Name           : DB Server
    Kind           : Server
Attributes:
     -> db.system.name: Str(oracle.db)
     -> oracle.db.instance.name: Str(POD1)
     -> oracle.db.name: Str(POD1)
     -> oracle.db.service: Str(MYATP_low.adb.oraclecloud.com)
     -> db.namespace: Str(POD1)
     -> db.response.status_code: Str(ORA-0)
     -> oracle.db.pdb: Str(MYATP)
```

The instrumentation scope for the Oracle server-side span is empty in this image. The stable identifiers to assert are currently `service.name=oracle-db`, span name `DB Server`, span kind `Server`, and parentage under the ODP.NET round-trip span.

The same trace appears in the ODP.NET client spans:

```text
InstrumentationScope Oracle.ManagedDataAccess.Core 23.26.200
Span #2
    Trace ID       : aff62c6fc94449d1d768ff83593476b7
    Parent ID      : e8bacd9eb696d907
    ID             : 632cea6d01e1fcb0
    Name           : SendExecuteRequest
    Kind           : Client
```

The 1000-iteration DML workload after the PDB GUID casing fix produced 1004 Oracle DB server-side spans in collector output:

```text
Name           : DB Server
Kind           : Server
Resource service.name: oracle-db
InstrumentationScope: empty
```

The simple select workload also produced app/ODP.NET span records across these scopes:

```text
InstrumentationScope Oracle.ManagedDataAccess.Core 23.26.200
InstrumentationScope OracleMdaSdkDemo 1.0.0
```

Oracle archived the same trace IDs in its diagnostic trace files:

```text
/u01/app/oracle/diag/rdbms/pod1/POD1/trace/POD1_dt01_647.trc:39:
KSTRC: Operation [traceid-63991d267e44349fe747719b75e058cd:parentid-75b1203475a3cc23] ... archived to: ksu_ops_POD1.trc

/u01/app/oracle/diag/rdbms/pod1/POD1/trace/POD1_dt00_645.trc:39:
KSTRC: Operation [traceid-8af78a326f57bdbc7e52acc020f111aa:parentid-63a8c0371452b672] ... archived to: ksu_ops_POD1.trc

/u01/app/oracle/diag/rdbms/pod1/POD1/trace/POD1_dt00_645.trc:42:
KSTRC: Operation [traceid-3ffeb44f3b856853c82c795542b03584:parentid-6a299fcd218c0152] ... archived to: ksu_ops_POD1.trc
```

The diagnostic trace archive also confirms that trace context reached the Oracle server and was associated with server-side work.

One confusing detail remains: on the current newer image, `DBMS_OBSERVABILITY` still reports runtime traces/logs as disabled even when a server-side span has been exported:

```text
Runtime Service "Traces": Disabled
Runtime Service "Logs": Disabled
Option "Capture traces": Enabled
Endpoint "https://otel-collector:4318/v1/traces": Enabled
```

Treat collector output as the source of truth for export validation in this local setup.

The 1000-iteration DML workload can be used to generate more database work:

```powershell
docker compose run --rm --no-deps -e WORKLOAD=dml -e ITERATIONS=1000 -e DELAY_SECONDS=0 -e FLUSH_SECONDS=60 -e LOG_EVERY=100 app
```

A previous 1000-iteration DML run before the PDB GUID casing fix produced 5010 app/ODP.NET spans across these scopes only:

```text
InstrumentationScope Oracle.ManagedDataAccess.Core 23.26.200
InstrumentationScope OracleMdaSdkDemo 1.0.0
```

The Oracle diagnostic archive contained 2008 `KSTRC` archived operations, confirming propagated context at volume. After the uppercase `disttrc` wallet fix, the same high-volume DML path exported Oracle DB server-side spans to the collector.

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
