param($deployEnvironment)

$platformRes = (az resource list --tag ard-resource-id=shared-container-registry | ConvertFrom-Json)
if (!$platformRes) {
    throw "Unable to find eligible platform container registry!"
}
if ($platformRes.Length -eq 0) {
    throw "Unable to find 'ANY' eligible platform container registry!"
}

$acr = ($platformRes | Where-Object { $_.tags.'ard-environment' -eq $deployEnvironment })
if (!$acr) {
    throw "Unable to find eligible prod container registry!"
}
$AcrName = $acr.Name

# Login to ACR
az acr login --name $AcrName
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to login to acr."
}

Push-Location DemoWebApp
az acr build --image demoapp -r $AcrName --file ./DemoWebApp/Dockerfile .
Pop-Location