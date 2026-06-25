#!/usr/bin/env bash
set -euo pipefail

cert_dir="${OTEL_DEMO_CERT_DIR:-/otel-demo-certs}"
root_cert="${cert_dir}/otel-demo-root-ca.crt"
server_cert="${cert_dir}/otel-collector.crt"

for cert in "$root_cert" "$server_cert"; do
  if [ ! -s "$cert" ]; then
    echo "Required certificate not found or empty: $cert" >&2
    exit 1
  fi
done

export JAVA_HOME="${JAVA_HOME:-/usr/lib/jvm/jdk-21.0.11-oracle-x64}"
export PATH="${JAVA_HOME}/bin:${PATH}"

read_pdb_guid_from_sql() {
  export TNS_ADMIN="${TNS_ADMIN:-/u01/app/oracle/wallets/tls_wallet}"
  local output

  output="$(sqlplus -L -s "ADMIN/${ADMIN_PASSWORD:?ADMIN_PASSWORD is required}@myatp_low" <<SQL 2>&1 || true
set heading off feedback off pagesize 0 linesize 4000 trimspool on verify off echo off
select rawtohex(guid) from v\$pdbs where name = sys_context('USERENV','CON_NAME');
exit
SQL
)"

  printf '%s\n' "$output" | tr -d '\r' | awk 'tolower($0) ~ /^[[:space:]]*[0-9a-f]{32}[[:space:]]*$/ { gsub(/[[:space:]]/, ""); print toupper($0); exit }'
}

read_pdb_guid_from_files() {
  find "${ORACLE_BASE}/oradata/${ORACLE_SID}" -maxdepth 1 -mindepth 1 -type d 2>/dev/null \
    | awk -F/ 'tolower($NF) ~ /^[0-9a-f]{32}$/ { print $NF; exit }'
}

pdb_guid=""
if [ "${ORACLE_OBSERVABILITY_WALLET_OFFLINE:-false}" != "true" ]; then
  pdb_guid="$(read_pdb_guid_from_sql)"
fi

if [ -z "$pdb_guid" ]; then
  pdb_guid="$(read_pdb_guid_from_files)"
fi

wallet_root="${WALLET_ROOT:-/u01/app/oracle/wallets}"

if [ -z "$wallet_root" ] || [ -z "$pdb_guid" ]; then
  echo "Could not determine wallet_root='$wallet_root' or pdb_guid='$pdb_guid'." >&2
  exit 1
fi

existing_pdb_guid_dir="$(
  find "$wallet_root" -maxdepth 1 -mindepth 1 -type d 2>/dev/null \
    | awk -F/ -v guid="$pdb_guid" 'tolower($NF) == tolower(guid) { print $NF; exit }'
)"

if [ -n "$existing_pdb_guid_dir" ]; then
  pdb_guid="$existing_pdb_guid_dir"
fi

wallet_dir="${wallet_root}/${pdb_guid}/disttrc"
mkdir -p "$wallet_dir"

if [ ! -f "${wallet_dir}/cwallet.sso" ]; then
  orapki wallet create -wallet "$wallet_dir" -auto_login_only
fi

add_trusted_cert() {
  local cert="$1"
  local output

  if output="$(orapki wallet add -wallet "$wallet_dir" -trusted_cert -cert "$cert" -auto_login_only 2>&1)"; then
    echo "$output"
    return 0
  fi

  if grep -q "PKI-04003" <<<"$output"; then
    echo "Trusted certificate already present in $wallet_dir: $cert"
    return 0
  fi

  echo "$output" >&2
  return 1
}

add_trusted_cert "$root_cert"
add_trusted_cert "$server_cert"

orapki wallet display -wallet "$wallet_dir" -summary
