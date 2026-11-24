#!/usr/bin/env bash
set -euo pipefail

GO_BIN="${FOX_GO:-go}"
"$GO_BIN" build \
  -tags "embed_inject,sqlite_only,embed_frontend_inject" \
  -ldflags "-s -w -X main.Mode=release" \
  -o fox-wx-channel.exe .

echo "built: fox-wx-channel.exe"
