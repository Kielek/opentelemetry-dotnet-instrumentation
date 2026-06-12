FROM ghcr.io/oracle/adb-free:26.2.4.2-26ai@sha256:7f7938ea0d11b500d427a090a28cc6913789e2a3e455439905b76712315dcd96

USER root

COPY certs/otel-demo-root-ca.crt /etc/pki/ca-trust/source/anchors/otel-demo-root-ca.crt
RUN chmod 0644 /etc/pki/ca-trust/source/anchors/otel-demo-root-ca.crt \
    && update-ca-trust extract

USER oracle
