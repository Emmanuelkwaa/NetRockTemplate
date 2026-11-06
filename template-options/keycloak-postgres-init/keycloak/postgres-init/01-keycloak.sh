#!/bin/bash
# Creates Keycloak's own database and login on the shared PostgreSQL server.
# The postgres image runs this only when its data volume is empty. For an existing volume,
# run it once by hand (see docs/deployment.md).
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    --set keycloak_password="$KEYCLOAK_DB_PASSWORD" <<'SQL'
CREATE ROLE keycloak LOGIN PASSWORD :'keycloak_password';
CREATE DATABASE keycloak OWNER keycloak;
REVOKE ALL ON DATABASE keycloak FROM PUBLIC;
SQL
