#!/usr/bin/env bash
# Verifies the template and generated projects across the init option matrix.
#
# Usage:
#   tools/init/verify-matrix.sh [template|full|blazor|matrix|all]   (default: all)
#
#   template  Build, test and frontend-check the template itself, then confirm it is unchanged
#   full      18 API stack x auth combinations (each Keycloak dev/prod mode counted): build, test,
#             migration, SvelteKit test/lint/check
#   blazor    blazor stack in both deploy modes: build, no backend left, no Svelte, ServiceDefaults
#   matrix    All 74 combinations: AppHost build, aspire publish or docker compose config (with values
#             supplied), Keycloak secrets and realm checks, and negative tests for a missing
#             production realm
#
# Environment:
#   VERIFY_DIR   Work folder (default: a new temp folder, removed on success)
#   FE_RUN       Command prefix that runs a shell command in a frontend folder:
#                "$FE_RUN <dir> '<command>'". Default runs pnpm locally.
#   ONLY         Space-separated combination filter, e.g. "auth=none" or "stack=api" or "kc-prod=managed"
#   KEEP         Set to keep each generated project; by default a project is removed once its checks pass
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCOPE="${1:-all}"
WORK="${VERIFY_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/netrock-verify.XXXXXX")}"
mkdir -p "$WORK"
LOG="$WORK/logs"
mkdir -p "$LOG"
FAILURES=()
# Tree state when the run starts; generating projects must leave it exactly as it was.
START_STATUS="$(cd "$ROOT" && git status --porcelain --untracked-files=all)"

fe_run() {
    local dir="$1" cmd="$2"
    if [[ -n "${FE_RUN:-}" ]]; then
        $FE_RUN "$dir" "$cmd"
    else
        (cd "$dir" && pnpm install --frozen-lockfile >/dev/null && bash -c "$cmd")
    fi
}

step() {
    local name="$1"; shift
    local logfile="$LOG/$(echo "$name" | tr ' /=' '___').log"
    if "$@" >"$logfile" 2>&1; then
        echo "  ok    $name"
    else
        echo "  FAIL  $name (log: $logfile)"
        FAILURES+=("$name")
    fi
}

matches_only() {
    local combo="$1"
    [[ -z "${ONLY:-}" ]] && return 0
    for term in $ONLY; do
        [[ " $combo " == *" $term "* ]] || return 1
    done
}

# A non-default test user password, so the secrets check can tell it apart from documentation.
TEST_PASSWORD="Verify-$(openssl rand -hex 8)"

generate() {
    local dir="$1"; shift
    "$ROOT/init.sh" --name VerifyApp --port 15000 --yes --no-aspire --no-commit --output "$dir" "$@"
}

# Keycloak dev/prod pairs for an auth mode: "-" (not Keycloak) or "dev/prod".
keycloak_modes() {
    if [[ "$1" == "keycloak" ]]; then echo "external/external managed/external external/managed managed/managed"; else echo "-"; fi
}

keycloak_args() {
    [[ "$1" == "-" ]] && return 0
    local dev="${1%/*}" prod="${1#*/}"
    echo "--keycloak-dev $dev --keycloak-prod $prod"
    [[ "$dev" == "managed" ]] && echo "--password $TEST_PASSWORD"
    return 0
}

keycloak_combo() {
    [[ "$1" == "-" ]] && return 0
    echo " kc-dev=${1%/*} kc-prod=${1#*/}"
}

# Removes a project after its checks pass, unless KEEP is set.
finish() {
    local dir="$1" failures_before="$2"
    if [[ -z "${KEEP:-}" && ${#FAILURES[@]} -eq $failures_before && -d "$dir" ]]; then
        rm -rf "${dir:?}"
    fi
}

# Fills every empty KEY= line of .env with a value, so required (:?) variables are supplied.
fill_env() {
    sed -E -e 's/^([A-Za-z_][A-Za-z0-9_]*_PORT)=$/\1=18080/' -e 's/^([A-Za-z_][A-Za-z0-9_]*)=$/\1=verify-value/' \
        "$1/.env.example" > "$1/.env"
}

# No Keycloak secret from AppHost user secrets may appear in a project file.
secrets_absent() {
    local dir="$1"
    local values
    values="$(cd "$dir/src/backend/VerifyApp.AppHost" && dotnet user-secrets list 2>/dev/null \
        | grep -E '^Parameters:keycloak-(client-secret|admin-password|test-user-password) = ' | sed 's/^[^=]*= //')"
    [[ -n "$values" ]] || { echo "no Keycloak secrets in user secrets"; return 1; }
    while IFS= read -r value; do
        if grep -rIlF --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj -e "$value" "$dir"; then
            echo "secret found in the files above"; return 1
        fi
    done <<< "$values"
}

# Aspire-generated compose must not publish a container's port 9000 (Keycloak's management
# interface) on the host. "expose" entries are fine: they stay on the compose network.
no_published_management_port() {
    local published
    published="$(awk '/^    ports:/{p=1;next} /^    [a-z_]+:/{p=0} p && /^      - /{print}' "$1/aspire-output/docker-compose.yaml")"
    if grep -E '(^|[":])9000"?$' <<< "$published"; then
        echo "port 9000 is published to the host"; return 1
    fi
}

prod_realm_has_no_users() {
    python3 -c "import json,sys; r=json.load(open(sys.argv[1])); sys.exit(1 if r.get('users') else 0)" "$1/keycloak/prod/realm.json"
}

dev_realm_has_test_user() {
    python3 -c "import json,sys; r=json.load(open(sys.argv[1])); u=r['users'][0]; sys.exit(0 if u['credentials'][0]['value']=='\${KC_TEST_USER_PASSWORD}' else 1)" "$1/keycloak/dev/realm.json"
}

# Negative test: own compose must refuse to start without the production realm.
compose_requires_realm() {
    local dir="$1"
    fill_env "$dir"
    sed -i.bak -E 's/^(Authentication__Keycloak__(Authority|ClientId|ClientSecret))=.*/\1=/' "$dir/.env"
    local output
    if output="$(cd "$dir" && docker compose config -q 2>&1)"; then
        echo "docker compose config succeeded without the Keycloak realm"; return 1
    fi
    grep -q "Set the Keycloak realm URL in .env" <<< "$output"
}

# Negative test: aspire deploy must stop before starting containers without the production realm.
# Removes the deployment state and any images it creates.
aspire_deploy_requires_realm() {
    local dir="$1" marker="$WORK/.deploy-marker"
    local images_before
    images_before="$(docker images -q --no-trunc api | sort -u)"
    touch "$marker"
    local output status=0
    output="$(cd "$dir" && env "Parameters__keycloak-client-secret=verify" "Parameters__public-url=http://localhost:1" \
        "Parameters__storage-endpoint=http://s" "Parameters__storage-access-key=a" "Parameters__storage-secret-key=b" \
        "Parameters__storage-bucket=c" "Parameters__storage-use-ssl=false" "Parameters__postgres-password=p" \
        aspire deploy --project src/backend/VerifyApp.AppHost/VerifyApp.AppHost.csproj -o aspire-output --non-interactive 2>&1)" || status=$?
    find "$HOME/.aspire/deployments" -mindepth 1 -maxdepth 1 -type d -newer "$marker" -exec rm -rf {} + 2>/dev/null || true
    # Images the deploy built before it stopped
    comm -13 <(echo "$images_before") <(docker images -q --no-trunc api | sort -u) | xargs -r docker rmi -f >/dev/null 2>&1 || true
    [[ $status -ne 0 ]] || { echo "aspire deploy succeeded without the Keycloak realm"; return 1; }
    grep -q "Parameters:keycloak-authority" <<< "$output" || { echo "$output" | tail -20; return 1; }
    if docker ps --format '{{.Names}}' | grep -q '^aspire-compose-.*-api-1$'; then
        echo "containers were started"; return 1
    fi
}

frontend_checks() {
    fe_run "$1/src/frontend" "pnpm run test && pnpm run lint && pnpm run check"
}

# Checks shared by every project with a Blazor frontend.
blazor_checks() {
    local combo="$1" dir="$2"
    step "$combo no svelte files" bash -c "! find '$dir' -name '*.svelte' -o -name 'package.json' -o -name 'pnpm-lock.yaml' | grep -q ."
    step "$combo no svelte or pnpm text" bash -c "! grep -rIil --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=lib -e svelte -e pnpm --exclude-dir=src '$dir'"
    step "$combo service defaults" grep -q 'builder.AddServiceDefaults();' "$dir/src/frontend/VerifyApp.UI/Program.cs"
    step "$combo default endpoints" grep -q 'app.MapDefaultEndpoints();' "$dir/src/frontend/VerifyApp.UI/Program.cs"
}

check_template() {
    echo "== Template"
    local before
    before="$(cd "$ROOT" && git status --porcelain)"
    step "template build" dotnet build "$ROOT/src/backend/NetRockTemplate.slnx"
    step "template test" dotnet test "$ROOT/src/backend/NetRockTemplate.slnx" -c Release
    step "template frontend" frontend_checks "$ROOT"
    step "template unchanged" test "$before" == "$(cd "$ROOT" && git status --porcelain)"
}

check_full() {
    echo "== Full checks (API stack x auth, each Keycloak mode)"
    for stack in api-svelte api-blazor api; do
        for auth in jwt keycloak none; do
            for kc in $(keycloak_modes "$auth"); do
                local combo="stack=$stack auth=$auth$(keycloak_combo "$kc")"
                matches_only "$combo deploy=aspire database=managed" || continue
                local dir="$WORK/full-$stack-$auth-${kc//\//-}"
                local backend="$dir/src/backend"
                local failures_before=${#FAILURES[@]}
                echo "-- $combo"
                # shellcheck disable=SC2046
                step "$combo generate" generate "$dir" --stack "$stack" --auth "$auth" --deploy aspire --database managed $(keycloak_args "$kc")
                [[ -d "$dir" ]] || continue
                step "$combo migration" bash -c "ls '$backend/VerifyApp.Infrastructure/Persistence/Migrations/'*_Initial.cs"
                step "$combo build" dotnet build "$backend/VerifyApp.slnx"
                step "$combo test" dotnet test "$backend/VerifyApp.slnx" -c Release
                if [[ "$stack" == "api-svelte" ]]; then
                    step "$combo frontend" frontend_checks "$dir"
                fi
                finish "$dir" "$failures_before"
            done
        done
    done
}

check_blazor() {
    echo "== Blazor only (both deploy modes)"
    for deploy in aspire compose; do
        local combo="stack=blazor deploy=$deploy"
        matches_only "$combo" || continue
        local dir="$WORK/blazor-$deploy"
        echo "-- $combo"
        step "$combo generate" generate "$dir" --stack blazor --deploy "$deploy"
        [[ -d "$dir" ]] || continue
        step "$combo build" dotnet build "$dir/src/backend/VerifyApp.slnx"
        step "$combo apphost build" dotnet build "$dir/src/backend/VerifyApp.AppHost"
        step "$combo no backend projects" bash -c "cd '$dir/src/backend' && test \"\$(ls -d VerifyApp.* | tr '\\n' ' ')\" == 'VerifyApp.AppHost VerifyApp.ServiceDefaults VerifyApp.slnx ' && ! test -e HealthProbe && ! test -e tests"
        step "$combo no backend references" bash -c "! grep -rIl --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=lib -e 'VerifyApp.WebApi' -e 'VerifyApp.Infrastructure' -e 'VerifyApp.Application' -e 'VerifyApp.Domain' -e 'VerifyApp.Shared' -e 'HealthProbe' -e 'dotnet-ef' -e 'Postgres' -e 'Minio' -e 'MailPit' '$dir'"
        step "$combo no migration tooling" test ! -e "$dir/.config/dotnet-tools.json"
        blazor_checks "$combo" "$dir"
    done
}

check_matrix() {
    echo "== Matrix (72 API stack combinations + 2 blazor)"
    for stack in api-svelte api-blazor api blazor; do
        for auth in jwt keycloak none; do
            for kc in $(keycloak_modes "$auth"); do
                for deploy in aspire compose; do
                    for database in managed external; do
                        local combo="stack=$stack auth=$auth$(keycloak_combo "$kc") deploy=$deploy database=$database"
                        local args=(--stack "$stack" --deploy "$deploy")
                        if [[ "$stack" == "blazor" ]]; then
                            # auth and database do not apply: one combination per deploy mode
                            [[ "$auth" == "jwt" && "$database" == "managed" ]] || continue
                            combo="stack=$stack deploy=$deploy"
                        else
                            # shellcheck disable=SC2207
                            args+=(--auth "$auth" --database "$database" $(keycloak_args "$kc"))
                        fi
                        matches_only "$combo" || continue
                        local dir="$WORK/m-$stack-$auth-${kc//\//-}-$deploy-$database"
                        local failures_before=${#FAILURES[@]}
                        echo "-- $combo"
                        step "$combo generate" generate "$dir" "${args[@]}" --no-migration
                        [[ -d "$dir" ]] || continue
                        step "$combo apphost build" dotnet build "$dir/src/backend/VerifyApp.AppHost"
                        if [[ "$stack" != "blazor" ]]; then
                            step "$combo webapi build" dotnet build "$dir/src/backend/VerifyApp.WebApi"
                        fi
                        step "$combo clean text" bash -c "! grep -rIl --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj -e '@init:' -e 'NetRockTemplate' '$dir'"
                        if [[ "$deploy" == "aspire" ]]; then
                            # Run from the project: aspire writes aspire.config.json into the current folder.
                            step "$combo aspire publish" bash -c "cd '$dir' && aspire publish --project src/backend/VerifyApp.AppHost/VerifyApp.AppHost.csproj -o aspire-output --non-interactive"
                            step "$combo no own compose" test ! -e "$dir/docker-compose.yaml"
                            step "$combo no published management port" no_published_management_port "$dir"
                        else
                            step "$combo compose config" bash -c "$(declare -f fill_env); fill_env '$dir' && cd '$dir' && docker compose config -q"
                        fi
                        case "$stack" in
                            api) step "$combo no frontend" test ! -e "$dir/src/frontend" ;;
                            api-blazor | blazor) blazor_checks "$combo" "$dir" ;;
                        esac
                        if [[ "$kc" != "-" ]]; then
                            local dev="${kc%/*}" prod="${kc#*/}"
                            if [[ "$dev" == "managed" ]]; then
                                step "$combo dev realm test user" dev_realm_has_test_user "$dir"
                                step "$combo no secrets in files" secrets_absent "$dir"
                            else
                                step "$combo no dev realm" test ! -e "$dir/keycloak/dev"
                            fi
                            if [[ "$prod" == "managed" ]]; then
                                step "$combo prod realm without users" prod_realm_has_no_users "$dir"
                            else
                                step "$combo no prod realm" test ! -e "$dir/keycloak/prod"
                                if [[ "$deploy" == "compose" ]]; then
                                    step "$combo compose refuses missing realm" compose_requires_realm "$dir"
                                elif [[ "$stack" == "api" && "$dev" == "external" && "$database" == "managed" ]]; then
                                    step "$combo aspire deploy refuses missing realm" aspire_deploy_requires_realm "$dir"
                                fi
                            fi
                        fi
                        finish "$dir" "$failures_before"
                    done
                done
            done
        done
    done
}

case "$SCOPE" in
    template) check_template ;;
    full) check_full ;;
    blazor) check_blazor ;;
    matrix) check_matrix ;;
    all)
        check_template; check_full; check_blazor; check_matrix
        echo "== Template after generating"
        step "template still unchanged" test "$START_STATUS" == "$(cd "$ROOT" && git status --porcelain --untracked-files=all)"
        ;;
    *) echo "Unknown scope: $SCOPE" >&2; exit 2 ;;
esac

echo
if [[ ${#FAILURES[@]} -gt 0 ]]; then
    echo "${#FAILURES[@]} check(s) failed. Work folder kept: $WORK"
    printf '  %s\n' "${FAILURES[@]}"
    exit 1
fi

echo "All checks passed."
if [[ -z "${VERIFY_DIR:-}" ]]; then
    rm -rf "$WORK"
fi
