import subprocess
import shutil

# please set current directory to com.superneko.desktopcast

# Build and generate
subprocess.run("cargo build -p desktopcast-bridge", shell=True, check=True, cwd="Native~")
subprocess.run("cargo run -p desktopcast-bindgen -- --library --config ./bridge/uniffi.toml --out-dir ./generated ./target/debug/desktopcast.dll", shell=True, check=True, cwd="Native~")

shutil.copy("Native~/target/debug/desktopcast.dll", "Plugins/desktopcast.dll")
shutil.copy("Native~/generated/desktopcast.cs", "Bindings/desktopcast.cs")
