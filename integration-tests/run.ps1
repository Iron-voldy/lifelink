param(
    [string]$EnvFile = ".env.e2e",
    [string]$ComposeProject = "lifelink-e2e",
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedEnvFile = Join-Path $repositoryRoot $EnvFile

if (-not (Test-Path -LiteralPath $resolvedEnvFile)) {
    throw "Missing $resolvedEnvFile. Copy .env.e2e.example to .env.e2e and replace its secrets."
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required to run the live PostgreSQL/API/agent integration test."
}

$adminPasswordLine = Get-Content -LiteralPath $resolvedEnvFile |
    Where-Object { $_ -match '^BOOTSTRAP_ADMIN_PASSWORD=' } |
    Select-Object -First 1
if (-not $adminPasswordLine) {
    throw "BOOTSTRAP_ADMIN_PASSWORD is required in $resolvedEnvFile."
}

$previousPassword = $env:LIFELINK_E2E_ADMIN_PASSWORD
$previousEvidence = $env:LIFELINK_E2E_EVIDENCE_DIR
$env:LIFELINK_E2E_ADMIN_PASSWORD = $adminPasswordLine.Substring($adminPasswordLine.IndexOf('=') + 1)
$env:LIFELINK_E2E_EVIDENCE_DIR = Join-Path $repositoryRoot "artifacts\e2e"

try {
    Push-Location $repositoryRoot
    docker compose --project-name $ComposeProject --env-file $resolvedEnvFile up --build --detach
    python integration-tests/cross_platform_workflow.py
}
finally {
    if (-not $KeepRunning -and (Get-Command docker -ErrorAction SilentlyContinue)) {
        docker compose --project-name $ComposeProject --env-file $resolvedEnvFile down
    }
    Pop-Location
    $env:LIFELINK_E2E_ADMIN_PASSWORD = $previousPassword
    $env:LIFELINK_E2E_EVIDENCE_DIR = $previousEvidence
}
