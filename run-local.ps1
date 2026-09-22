param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $CliArguments
)

$ErrorActionPreference = "Stop"
$profile = (Resolve-Path (Join-Path $PSScriptRoot "profiles/header-auth/sample.props")).Path
$project = Join-Path $PSScriptRoot "src/AIHappey.Cli/AIHappey.Cli.csproj"

dotnet run --project $project -p:CliProfile="$profile" -- @CliArguments
exit $LASTEXITCODE

