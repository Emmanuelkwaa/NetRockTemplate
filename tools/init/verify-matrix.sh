#!/usr/bin/env bash
# Verifies the template and generated projects across the init option matrix.
#
# Usage:
#   tools/init/verify-matrix.sh [template|full|blazor|matrix|all]   (default: all)
#
#   template  Build, test and frontend-check the template itself, then confirm it is unchanged
#   full      9 API stack x auth combinations: build, test, migration, SvelteKit test/lint/check
#   blazor    blazor stack in both deploy modes: build, no backend left, no Svelte, ServiceDefaults
#   matrix    All 38 combinations: AppHost build, aspire publish or docker compose config
#
# Environment:
#   VERIFY_DIR   Work folder (default: a new temp folder, removed on success)
#   FE_RUN       Command prefix that runs a shell command in a frontend folder:
#                "$FE_RUN <dir> '<command>'". Default runs pnpm locally.
#   ONLY         Space-separated combination filter, e.g. "auth=none" or "stack=api"
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCOPE="${1:-all}"
WORK="${VERIFY_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/netrock-verify.XXXXXX")}"
mkdir -p "$WORK"
LOG="$WORK/logs"
mkdir -p "$LOG"
FAILURES=()

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

generate() {
    local dir="$1"; shift
    "$ROOT/init.sh" --name VerifyApp --port 15000 --yes --no-aspire --no-commit --output "$dir" "$@"
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
    echo "== Full checks (API stack x auth)"
    for stack in api-svelte api-blazor api; do
        for auth in jwt keycloak none; do
            local combo="stack=$stack auth=$auth"
            matches_only "$combo deploy=aspire database=managed" || continue
            local dir="$WORK/full-$stack-$auth"
            local backend="$dir/src/backend"
            echo "-- $combo"
            step "$combo generate" generate "$dir" --stack "$stack" --auth "$auth" --deploy aspire --database managed
            [[ -d "$dir" ]] || continue
            step "$combo migration" bash -c "ls '$backend/VerifyApp.Infrastructure/Persistence/Migrations/'*_Initial.cs"
            step "$combo build" dotnet build "$backend/VerifyApp.slnx"
            step "$combo test" dotnet test "$backend/VerifyApp.slnx" -c Release
            if [[ "$stack" == "api-svelte" ]]; then
                step "$combo frontend" frontend_checks "$dir"
            fi
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
    echo "== Matrix (36 API stack combinations + 2 blazor)"
    for stack in api-svelte api-blazor api blazor; do
        for auth in jwt keycloak none; do
            for deploy in aspire compose; do
                for database in managed external; do
                    local combo="stack=$stack auth=$auth deploy=$deploy database=$database"
                    local args=(--stack "$stack" --deploy "$deploy")
                    if [[ "$stack" == "blazor" ]]; then
                        # auth and database do not apply: one combination per deploy mode
                        [[ "$auth" == "jwt" && "$database" == "managed" ]] || continue
                        combo="stack=$stack deploy=$deploy"
                    else
                        args+=(--auth "$auth" --database "$database")
                    fi
                    matches_only "$combo" || continue
                    local dir="$WORK/m-$stack-$auth-$deploy-$database"
                    echo "-- $combo"
                    step "$combo generate" generate "$dir" "${args[@]}" --no-migration
                    [[ -d "$dir" ]] || continue
                    step "$combo apphost build" dotnet build "$dir/src/backend/VerifyApp.AppHost"
                    if [[ "$stack" != "blazor" ]]; then
                        step "$combo webapi build" dotnet build "$dir/src/backend/VerifyApp.WebApi"
                    fi
                    step "$combo clean text" bash -c "! grep -rIl --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj -e '@init:' -e 'NetRockTemplate' '$dir'"
                    if [[ "$deploy" == "aspire" ]]; then
                        step "$combo aspire publish" aspire publish --project "$dir/src/backend/VerifyApp.AppHost/VerifyApp.AppHost.csproj" -o "$dir/aspire-output" --non-interactive
                        step "$combo no own compose" test ! -e "$dir/docker-compose.yaml"
                    else
                        step "$combo compose config" bash -c "cd '$dir' && cp .env.example .env && docker compose config -q"
                    fi
                    case "$stack" in
                        api) step "$combo no frontend" test ! -e "$dir/src/frontend" ;;
                        api-blazor | blazor) blazor_checks "$combo" "$dir" ;;
                    esac
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
    all) check_template; check_full; check_blazor; check_matrix ;;
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
