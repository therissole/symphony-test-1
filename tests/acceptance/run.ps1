[CmdletBinding()]
param(
    [int]$GatewayPort = 18081,
    [int]$KeycloakPort = 18180,
    [int]$PostgresPort = 15432,
    [int]$OpenFgaPort = 18082,
    [string]$ProjectName = "acceptance-$PID",
    [string]$TestFilter = 'TestCategory=Acceptance',
    [string]$BlameHangTimeout = '5m'
)

$ErrorActionPreference = 'Stop'
$solutionRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$acceptanceProject = Join-Path $solutionRoot 'tests\acceptance\symphony-test-1.AcceptanceTests\symphony-test-1.AcceptanceTests.csproj'
$previousGatewayPort = $env:SYMPHONY_API_PORT
$previousKeycloakPort = $env:SYMPHONY_KEYCLOAK_PORT
$previousPostgresPort = $env:SYMPHONY_POSTGRES_PORT
$previousOpenFgaPort = $env:SYMPHONY_OPENFGA_PORT

try {
    $env:SYMPHONY_API_PORT = $GatewayPort
    $env:SYMPHONY_KEYCLOAK_PORT = $KeycloakPort
    $env:SYMPHONY_POSTGRES_PORT = $PostgresPort
    $env:SYMPHONY_OPENFGA_PORT = $OpenFgaPort

    & docker compose -p $ProjectName -f (Join-Path $solutionRoot 'docker-compose.yml') -f (Join-Path $PSScriptRoot 'docker-compose.acceptance.yml') up --build --detach
    if ($LASTEXITCODE -ne 0) { throw 'The acceptance environment did not start.' }

    $healthUri = "http://localhost:$GatewayPort/api/health"
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(4)
    do {
        try {
            if ((Invoke-WebRequest -UseBasicParsing $healthUri -TimeoutSec 5).StatusCode -eq 200) { break }
        }
        catch { }
        Start-Sleep -Seconds 2
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ((Invoke-WebRequest -UseBasicParsing $healthUri -TimeoutSec 5).StatusCode -ne 200) {
        throw "The acceptance environment did not become healthy at $healthUri."
    }

    $env:ACCEPTANCE_BASE_URL = "http://localhost:$GatewayPort/"
    $env:ACCEPTANCE_TOKEN_ENDPOINT = "http://localhost:$KeycloakPort/realms/symphony/protocol/openid-connect/token"
    $env:ACCEPTANCE_CLIENT_ID = 'acceptance-tests'
    $env:ACCEPTANCE_CLIENT_SECRET = 'acceptance-tests-local-secret'
    $env:ACCEPTANCE_BROWSER_USERNAME = 'acceptance-browser'
    $env:ACCEPTANCE_BROWSER_PASSWORD = 'Acceptance!12345'
    $env:ACCEPTANCE_SUPERUSER_USERNAME = 'acceptance-browser'
    $env:ACCEPTANCE_SUPERUSER_PASSWORD = 'Acceptance!12345'
    $env:ACCEPTANCE_STANDARD_USER_USERNAME = 'acceptance-standard-user'
    $env:ACCEPTANCE_STANDARD_USER_PASSWORD = 'AcceptanceStandard!12345'

    & dotnet restore $acceptanceProject --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'The acceptance project did not restore from its lock file.' }

    & dotnet build $acceptanceProject --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'The acceptance project did not build.' }

    & (Join-Path (Split-Path $acceptanceProject) 'bin\Release\net10.0\playwright.ps1') install chromium
    if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }

    & dotnet test $acceptanceProject `
        --configuration Release `
        --no-build `
        --no-restore `
        --filter $TestFilter `
        --blame-hang `
        --blame-hang-timeout $BlameHangTimeout `
        --logger 'console;verbosity=normal' `
        --logger 'trx;LogFileName=acceptance-test-results.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Acceptance tests failed.' }
}
finally {
    & docker compose -p $ProjectName -f (Join-Path $solutionRoot 'docker-compose.yml') -f (Join-Path $PSScriptRoot 'docker-compose.acceptance.yml') down --volumes --remove-orphans
    $env:SYMPHONY_API_PORT = $previousGatewayPort
    $env:SYMPHONY_KEYCLOAK_PORT = $previousKeycloakPort
    $env:SYMPHONY_POSTGRES_PORT = $previousPostgresPort
    $env:SYMPHONY_OPENFGA_PORT = $previousOpenFgaPort
}
