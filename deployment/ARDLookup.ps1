param($ResourceId, $ResourceEnvironment)

$ErrorActionPreference = "Stop"

$platformRes = (az resource list --tag ard-resource-id=$ResourceId | ConvertFrom-Json)
if (!$platformRes) {
    throw "Unable to find eligible ARD resource!"
}
if ($platformRes.Length -eq 0) {
    throw "Unable to find 'ANY' eligible ARD resource!"
}

$foundRes = ($platformRes | Where-Object { $_.tags.'ard-environment' -eq $ResourceEnvironment })
if (!$foundRes) {
    throw "Unable to find eligible $ResourceEnvironment resource!"
}

$ardResourceName = $foundRes.Name
$ardResourceId = $foundRes.Id
Write-Host "::set-output name=resourceName::$ardResourceName"
Write-Host "::set-output name=resourceId::$ardResourceId"