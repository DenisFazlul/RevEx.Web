$ErrorActionPreference = 'Stop'
dotnet run --project "$PSScriptRoot/ContentSubscriptionChecks"
if ($LASTEXITCODE) { throw 'Subscription checks failed' }
