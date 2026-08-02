#!/bin/bash
# Creates the role the API connects as (ADR-0029).
#
# This exists for one reason: a Postgres SUPERUSER bypasses row-level security
# unconditionally — FORCE ROW LEVEL SECURITY does not constrain it. If the API
# kept connecting as `postgres`, every tenant policy would be inert and the
# isolation would be theatre. So the API gets a dedicated NOSUPERUSER NOBYPASSRLS
# role, while migrations and dev seeding stay on the owner connection.
#
# Runs once, at container first-init (docker-entrypoint-initdb.d).
set -e

: "${APP_DB_ROLE:?APP_DB_ROLE must be set}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD must be set}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "postgres" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '${APP_DB_ROLE}') THEN
            CREATE ROLE ${APP_DB_ROLE}
                LOGIN PASSWORD '${APP_DB_PASSWORD}'
                NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOINHERIT;
        END IF;
    END
    \$\$;

    GRANT USAGE ON SCHEMA public TO ${APP_DB_ROLE};

    -- Tables do not exist yet (migrations run later), so grant forward: anything
    -- the owner creates in this schema from now on is readable/writable by the
    -- app role. DDL stays with the owner — the app role can never alter a policy.
    ALTER DEFAULT PRIVILEGES FOR ROLE ${POSTGRES_USER} IN SCHEMA public
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ${APP_DB_ROLE};
    ALTER DEFAULT PRIVILEGES FOR ROLE ${POSTGRES_USER} IN SCHEMA public
        GRANT USAGE, SELECT ON SEQUENCES TO ${APP_DB_ROLE};
EOSQL
