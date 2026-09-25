# Desktopcast Native Library
- MSWindows only
- Requires system-installed gstreamer

## Build and generate
Using `build.py` is easiest way: `cd com..superneko.desktopcast && python .\Tools~\build.py`

## Build
1. You need to set PATH: `$env:PATH='C:\Program Files\gstreamer\1.0\msvc_x86_64\bin;' + $env:PATH`
2. Then cargo build

## Generate binding
1. You need to build bridge first. `cargo build` can be swapped with `cargo build -p desktopcast-bridge`
2. Then run bindgen. `cargo run -p desktopcast-bindgen -- --library --config .\bridge\uniffi.toml --out-dir .\generated .\target\debug\desktopcast.dll`
