[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $DocumentPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$document = Get-Content -LiteralPath $DocumentPath -Raw | ConvertFrom-Json -AsHashtable
$failures = [System.Collections.Generic.List[string]]::new()
$operationIds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal)
$httpMethods = [System.Collections.Generic.HashSet[string]]::new(
    [string[]] @('delete', 'get', 'head', 'options', 'patch', 'post', 'put', 'trace'),
    [System.StringComparer]::Ordinal)
$schemaNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]] $document.components.schemas.Keys,
    [System.StringComparer]::Ordinal)

function Test-StringValue {
    param(
        [System.Collections.IDictionary] $Value,
        [string] $Property,
        [string] $Expected
    )

    return $Value.Contains($Property) -and $Value[$Property] -ceq $Expected
}

function Require-Text {
    param(
        [System.Collections.IDictionary] $Value,
        [string] $Property,
        [string] $Location
    )

    if (-not $Value.Contains($Property) -or
        [string]::IsNullOrWhiteSpace([string] $Value[$Property])) {
        $failures.Add("$Location must declare a non-empty $Property.")
    }
}

function Require-Uuid {
    param(
        [System.Collections.IDictionary] $Value,
        [string] $Location
    )

    if (-not (Test-StringValue $Value 'type' 'string') -or
        -not (Test-StringValue $Value 'format' 'uuid')) {
        $failures.Add("$Location must use type string and format uuid.")
    }
}

function Find-UnresolvedSchemaReference {
    param(
        [object] $Value,
        [string] $Location
    )

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($entry in $Value.GetEnumerator()) {
            if ($entry.Key -ceq '$ref' -and
                $entry.Value -is [string] -and
                $entry.Value.StartsWith(
                    '#/components/schemas/',
                    [System.StringComparison]::Ordinal)) {
                $schemaName = $entry.Value.Substring('#/components/schemas/'.Length)
                if (-not $schemaNames.Contains($schemaName)) {
                    $failures.Add("$Location has unresolved schema reference '$($entry.Value)'.")
                }
            }

            Find-UnresolvedSchemaReference $entry.Value "$Location.$($entry.Key)"
        }

        return
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $index = 0
        foreach ($item in $Value) {
            Find-UnresolvedSchemaReference $item "$Location[$index]"
            $index++
        }
    }
}

if (-not (Test-StringValue $document 'openapi' '3.1.1')) {
    $failures.Add('The document must use OpenAPI 3.1.1.')
}

foreach ($path in $document.paths.GetEnumerator()) {
    foreach ($operation in $path.Value.GetEnumerator()) {
        if (-not $httpMethods.Contains($operation.Key)) {
            continue
        }

        $location = "$($operation.Key.ToUpperInvariant()) $($path.Key)"
        Require-Text $operation.Value 'summary' $location
        Require-Text $operation.Value 'description' $location
        Require-Text $operation.Value 'operationId' $location

        if ($operation.Value.Contains('operationId') -and
            -not $operationIds.Add([string] $operation.Value.operationId)) {
            $failures.Add(
                "$location duplicates operationId '$($operation.Value.operationId)'.")
        }

        if (-not $operation.Value.Contains('tags') -or $operation.Value.tags.Count -eq 0) {
            $failures.Add("$location must declare at least one tag.")
        }

        $successfulResponse = $operation.Value.responses.Keys |
            Where-Object { $_.StartsWith('2', [System.StringComparison]::Ordinal) } |
            Select-Object -First 1
        if ($null -eq $successfulResponse) {
            $failures.Add("$location must declare a successful response.")
        }

        if ($operation.Value.Contains('parameters')) {
            foreach ($parameter in $operation.Value.parameters) {
                $parameterName = [string] $parameter.name
                Require-Text $parameter 'description' "$location parameter $parameterName"

                if ((Test-StringValue $parameter 'in' 'query') -and
                    $parameterName -cne (
                        $parameterName.Substring(0, 1).ToLowerInvariant() +
                        $parameterName.Substring(1))) {
                    $failures.Add(
                        "$location query parameter '$parameterName' must be lower camel case.")
                }

                if ((Test-StringValue $parameter 'in' 'path') -and
                    (-not $parameter.Contains('required') -or
                     $parameter.required -isnot [bool] -or
                     -not $parameter.required)) {
                    $failures.Add(
                        "$location path parameter '$parameterName' must be required.")
                }

                if ($parameterName -in @('id', 'languageId')) {
                    Require-Uuid $parameter.schema "$location parameter $parameterName"
                }
            }
        }

        $administrationOperation =
            $path.Key.StartsWith('/api/languages', [System.StringComparison]::Ordinal) -or
            $path.Key.StartsWith('/api/greetings', [System.StringComparison]::Ordinal)
        if ($administrationOperation) {
            if (-not $operation.Value.Contains('security')) {
                $failures.Add("$location must declare bearer security.")
            }

            if (-not $operation.Value.responses.Contains('401')) {
                $failures.Add("$location must declare its 401 response.")
            }
        }
    }
}

foreach ($schema in $document.components.schemas.GetEnumerator()) {
    if ($schema.Key -in @('LanguageId', 'GreetingId')) {
        Require-Uuid $schema.Value $schema.Key
    }

    if ($schema.Key.EndsWith('ProblemDetails', [System.StringComparison]::Ordinal) -or
        -not $schema.Value.Contains('properties')) {
        continue
    }

    foreach ($property in $schema.Value.properties.GetEnumerator()) {
        Require-Text $property.Value 'description' "$($schema.Key).$($property.Key)"

        $lowerCamelProperty =
            $property.Key.Substring(0, 1).ToLowerInvariant() +
            $property.Key.Substring(1)
        if ($property.Key -cne $lowerCamelProperty) {
            $failures.Add(
                "$($schema.Key).$($property.Key) must be lower camel case.")
        }

        if ($property.Key -in @('id', 'languageId')) {
            $expectedSchema = if (
                $property.Key -ceq 'id' -and
                $schema.Key.Contains('Greeting', [System.StringComparison]::Ordinal)
            ) {
                'GreetingId'
            } else {
                'LanguageId'
            }

            if (-not (Test-StringValue `
                    $property.Value `
                    '$ref' `
                    "#/components/schemas/$expectedSchema")) {
                $failures.Add(
                    "$($schema.Key).$($property.Key) must reference $expectedSchema.")
            }
        }
    }
}

Find-UnresolvedSchemaReference $document '$'

foreach ($failure in $failures | Sort-Object) {
    Write-Error $failure -ErrorAction Continue
}

if ($failures.Count -gt 0) {
    throw "OpenAPI lint failed with $($failures.Count) violation(s)."
}

Write-Host (
    "OpenAPI lint passed: {0} operations, {1} schemas." -f
    $operationIds.Count,
    $schemaNames.Count)
