#!/usr/bin/env bash
# Verifies the template and generated projects across the init option matrix.
#
# Usage:
#   tools/init/verify-matrix.sh [template|full|matrix|all]   (default: all)
#
#   template  Build, test and frontend-check the template itself, then confirm it is unchanged
#   full      6 auth x frontend combinations: build, test, migration, frontend test/lint/check
#   matrix    All 24 combinations: AppHost build, aspire publish or docker compose config
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
    echo "== Full checks (auth x frontend)"
    for auth in jwt keycloak none; do
        for frontend in yes no; do
            local combo="auth=$auth frontend=$frontend"
            matches_only "$combo deploy=aspire database=managed" || continue
            local dir="$WORK/full-$auth-$frontend"
            local backend="$dir/src/backend"
            echo "-- $combo"
            step "$combo generate" generate "$dir" --auth "$auth" --frontend "$frontend" --deploy aspire --database managed
            [[ -d "$dir" ]] || continue
            step "$combo migration" bash -c "ls '$backend/VerifyApp.Infrastructure/Persistence/Migrations/'*_Initial.cs"
            step "$combo build" dotnet build "$backend/VerifyApp.slnx"
            step "$combo test" dotnet test "$backend/VerifyApp.slnx" -c Release
            if [[ "$frontend" == "yes" ]]; then
                step "$combo frontend" frontend_checks "$dir"
            fi
        done
    done
}

check_matrix() {
    echo "== Matrix (all 24 combinations)"
    for auth in jwt keycloak none; do
        for deploy in aspire compose; do
            for database in managed external; do
                for frontend in yes no; do
                    local combo="auth=$auth deploy=$deploy database=$database frontend=$frontend"
                    matches_only "$combo" || continue
                    local dir="$WORK/m-$auth-$deploy-$database-$frontend"
                    echo "-- $combo"
                    step "$combo generate" generate "$dir" --auth "$auth" --deploy "$deploy" --database "$database" --frontend "$frontend" --no-migration
                    [[ -d "$dir" ]] || continue
                    step "$combo apphost build" dotnet build "$dir/src/backend/VerifyApp.AppHost"
                    step "$combo webapi build" dotnet build "$dir/src/backend/VerifyApp.WebApi"
                    step "$combo clean text" bash -c "! grep -rIl --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj -e '@init:' -e 'NetRockTemplate' '$dir'"
                    if [[ "$deploy" == "aspire" ]]; then
                        step "$combo aspire publish" aspire publish --project "$dir/src/backend/VerifyApp.AppHost/VerifyApp.AppHost.csproj" -o "$dir/aspire-output" --non-interactive
                        step "$combo no own compose" test ! -e "$dir/docker-compose.yaml"
                    else
                        step "$combo compose config" bash -c "cd '$dir' && cp .env.example .env && docker compose config -q"
                    fi
                    if [[ "$frontend" == "no" ]]; then
                        step "$combo no frontend" test ! -e "$dir/src/frontend"
                    fi
                done
            done
        done
    done
}

case "$SCOPE" in
    template) check_template ;;
    full) check_full ;;
    matrix) check_matrix ;;
    all) check_template; check_full; check_matrix ;;
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
