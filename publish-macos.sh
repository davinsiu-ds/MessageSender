#!/usr/bin/env zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

RUNTIME="${1:-osx-arm64}"
CONFIG="${CONFIG:-Release}"
VERSION="${VERSION:-2.0.1}"

PROJECT="./MessageSender.Desktop/MessageSender.Desktop.csproj"
PUBLISH_DIR="./MessageSender.Desktop/bin/${CONFIG}/net8.0/${RUNTIME}/publish"

dotnet publish "$PROJECT" \
  -c "$CONFIG" \
  -r "$RUNTIME" \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -p:IncludeAllContentForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:Version="$VERSION"

chmod 777 "$PUBLISH_DIR/MessageSender.Desktop"

echo "Published output: ${PUBLISH_DIR}"
