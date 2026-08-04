#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
FRAMEWORK="$ROOT/upstream-celeste-wasm/frontend/public/_framework"
PUBLISH="$ROOT/upstream-celeste-wasm/loader/bin/Release/net10.0/publish/wwwroot/_framework"
ASSETS="$ROOT/assets/www/_framework"

rm -rf "$FRAMEWORK"
cp -r "$PUBLISH" "$FRAMEWORK"

python3 - "$FRAMEWORK" <<'PY'
from pathlib import Path
import sys

framework = Path(sys.argv[1])
for path in framework.glob("dotnet.native.*.js"):
    text = path.read_text()
    text = text.replace(
        "var offscreenCanvases={};",
        'var offscreenCanvases={};if(globalThis.window&& !window.TRANSFERRED_CANVAS){transferredCanvasNames=[".canvas"];window.TRANSFERRED_CANVAS=true;}',
    )
    text = text.replace(
        "var offscreenCanvases = {};",
        'var offscreenCanvases={};if(globalThis.window&& !window.TRANSFERRED_CANVAS){transferredCanvasNames=[".canvas"];window.TRANSFERRED_CANVAS=true;}',
    )
    text = text.replace(
        "return runEmAsmFunction(code, sigPtr, argbuf);",
        "return runMainThreadEmAsm(code, sigPtr, argbuf, 1);",
    )
    path.write_text(text)

for path in framework.glob("dotnet.runtime.*.js"):
    path.write_text(path.read_text().replace("this.appendULeb(32768)", "this.appendULeb(65535)"))
PY

(
    cd "$FRAMEWORK"
    for wasm in dotnet.native.*.wasm; do
        [ -e "$wasm" ] || continue
        split -b20M -d -a1 "$wasm" "$wasm"
        rm "$wasm"
    done
)

rm -rf "$ASSETS"
cp -r "$FRAMEWORK" "$ASSETS"
