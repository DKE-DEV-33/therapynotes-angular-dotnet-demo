#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DOTNET_ROOT="$ROOT_DIR/.dotnet"
export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"

exec "$DOTNET_ROOT/dotnet" "$@"

