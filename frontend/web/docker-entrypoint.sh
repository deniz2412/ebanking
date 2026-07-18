#!/bin/sh
# Runs from nginx:alpine's /docker-entrypoint.d/ before nginx starts. Regenerates the SPA's
# runtime config from environment so one image works in any environment (dev / compose / k8s).
set -e

: "${KEYCLOAK_URL:=http://localhost:8180}"
: "${KEYCLOAK_REALM:=ebanking}"
: "${KEYCLOAK_CLIENT_ID:=ebanking-frontend}"

cat > /usr/share/nginx/html/config.json <<EOF
{
  "keycloakUrl": "${KEYCLOAK_URL}",
  "realm": "${KEYCLOAK_REALM}",
  "clientId": "${KEYCLOAK_CLIENT_ID}"
}
EOF
