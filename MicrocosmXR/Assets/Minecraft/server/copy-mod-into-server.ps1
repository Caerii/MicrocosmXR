# Copy the built Microcosm Streamer mod from fabric-mod into this server's mods folder.
# Run from this server folder. Requires the mod to be built first:
#   cd fabric-mod; .\gradlew.bat build

$ErrorActionPreference = "Stop"
$serverDir = $PSScriptRoot
# Repo root: server -> Minecraft -> Assets -> MicrocosmXR -> repo root (contains fabric-mod)
$microcosmRoot = (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $serverDir)))
$repoRoot = Split-Path -Parent $microcosmRoot
$modJar = Join-Path $repoRoot "fabric-mod\build\libs\microcosm-streamer-0.1.0.jar"
$modsDir = Join-Path $serverDir "mods"

if (-not (Test-Path $modJar)) {
    Write-Error "Mod JAR not found. Build it first: cd fabric-mod; .\gradlew.bat build"
}
if (-not (Test-Path $modsDir)) {
    New-Item -ItemType Directory -Path $modsDir | Out-Null
}
Copy-Item -Path $modJar -Destination $modsDir -Force
Write-Host "Copied microcosm-streamer-0.1.0.jar into $modsDir"
