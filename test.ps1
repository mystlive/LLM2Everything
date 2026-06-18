Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
dotnet build (Join-Path $root "LLM2Everything.slnx") --no-restore
dotnet run --project (Join-Path $root "LLM2Everything.Tests\LLM2Everything.Tests.csproj") --no-restore
