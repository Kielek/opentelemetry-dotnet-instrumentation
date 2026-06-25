FROM ghcr.io/oracle/adb-free:26.5.4.2-26ai@sha256:bd532de37372b569090ae7e3c8b14d93d57eec8fc74aba2b115f0d39ee91b98e

USER root

COPY certs/otel-demo-root-ca.crt /etc/pki/ca-trust/source/anchors/otel-demo-root-ca.crt
COPY configure-observability-wallet.sh /u01/scripts/configure-observability-wallet.sh
COPY entrypoint-with-observability-wallet.sh /u01/scripts/entrypoint-with-observability-wallet.sh
RUN chmod 0644 /etc/pki/ca-trust/source/anchors/otel-demo-root-ca.crt \
    && chmod 0755 /u01/scripts/configure-observability-wallet.sh \
    && chmod 0755 /u01/scripts/entrypoint-with-observability-wallet.sh \
    && update-ca-trust extract

USER oracle

ENTRYPOINT ["/u01/scripts/entrypoint-with-observability-wallet.sh"]
