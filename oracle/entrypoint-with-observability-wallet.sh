#!/usr/bin/env bash
set -euo pipefail

if [ ! -f "${ORACLE_ROOT}/.unzipped_pod1" ]; then
  unzip -o "${ORACLE_ROOT}/POD1.zip" -d /
  touch "${ORACLE_ROOT}/.unzipped_pod1"
  rm -rf "${ORACLE_ROOT}/POD1.zip"
fi

if [ "${ORACLE_ENABLE_KSTRC:-true}" = "true" ]; then
  sqlplus / as sysdba <<'SQL'
whenever sqlerror exit failure
startup nomount;
alter system set "_kstrc_service_mask"=0 scope=spfile;
shutdown abort;
exit;
SQL
fi

exec /u01/scripts/entrypoint.sh
