[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$image = 'openfga/cli:v0.7.8'

function Invoke-OpenFgaCli {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & docker run --rm `
        --mount "type=bind,source=$repositoryRoot,target=/workspace,readonly" `
        --workdir /workspace `
        $image `
        @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "OpenFGA CLI command failed with exit code $LASTEXITCODE."
    }
}

Invoke-OpenFgaCli -Arguments @('model', 'validate', '--file', 'openfga/authorization-model.fga')
$generatedModelJson = (Invoke-OpenFgaCli -Arguments @(
        'model', 'transform', '--file', 'openfga/authorization-model.fga', '--output-format', 'json'
    )) -join "`n"
$committedModelJson = Get-Content -Raw -Path (Join-Path $repositoryRoot 'openfga/authorization-model.json')

if (-not [string]::Equals(
        $generatedModelJson.Trim(),
        $committedModelJson.Trim(),
        [System.StringComparison]::Ordinal)) {
    throw 'openfga/authorization-model.json is out of date. Regenerate it from authorization-model.fga.'
}

Invoke-OpenFgaCli -Arguments @('model', 'test', '--tests', 'openfga/authorization-model.tests.fga.yaml')
