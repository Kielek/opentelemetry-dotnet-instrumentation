#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

rm -rf certs/ca
mkdir -p certs/ca/newcerts
touch certs/ca/index.txt
printf '1000\n' > certs/ca/serial
printf '1000\n' > certs/ca/crlnumber

openssl genrsa -out certs/otel-demo-root-ca.key 4096
openssl req \
  -x509 \
  -new \
  -nodes \
  -key certs/otel-demo-root-ca.key \
  -sha256 \
  -days 3650 \
  -subj "/CN=otel-demo-root-ca" \
  -out certs/otel-demo-root-ca.crt \
  -extensions v3_ca \
  -config openssl-ca.cnf

openssl genrsa -out certs/otel-collector.key 2048
openssl req \
  -new \
  -key certs/otel-collector.key \
  -subj "/CN=otel-collector" \
  -out certs/otel-collector.csr

openssl ca \
  -batch \
  -config openssl-ca.cnf \
  -extensions server_cert \
  -days 3650 \
  -notext \
  -md sha256 \
  -in certs/otel-collector.csr \
  -out certs/otel-collector.crt

openssl ca \
  -batch \
  -config openssl-ca.cnf \
  -gencrl \
  -out certs/otel-demo-root-ca.crl

chmod 0644 \
  certs/otel-demo-root-ca.crt \
  certs/otel-demo-root-ca.crl \
  certs/otel-collector.crt \
  certs/otel-collector.key
