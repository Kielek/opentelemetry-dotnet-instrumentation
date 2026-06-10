#!/usr/bin/env bash
set -euo pipefail

wallet_cert="${ORACLE_WALLET_SOURCE:-/oracle-wallets/tls_wallet}/adb_container.cert"
system_cert="/usr/local/share/ca-certificates/oracle-adb-free.crt"

if [ -f "$wallet_cert" ] && command -v update-ca-certificates >/dev/null 2>&1; then
  cp "$wallet_cert" "$system_cert"
  update-ca-certificates >/dev/null
fi

exec dotnet OracleMdaSdkDemo.dll
