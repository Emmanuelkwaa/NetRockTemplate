#!/usr/bin/env bash
# Creates a new project from this template. All logic lives in tools/init/init.cs.
# Usage: ./init.sh --help
set -euo pipefail
cd "$(dirname "$0")"
if ! command -v dotnet >/dev/null 2>&1; then
    echo "The .NET 10 SDK is required: https://dotnet.microsoft.com/download" >&2
    exit 1
fi
exec dotnet run tools/init/init.cs -- "$@"
