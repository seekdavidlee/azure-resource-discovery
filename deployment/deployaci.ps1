param($ResourceGroupName, $DeployEnvironment)

$platformRes = (az resource list --tag ard-resource-id=shared-container-registry | ConvertFrom-Json)
if (!$platformRes) {
    throw "Unable to find eligible platform container registry!"
}
if ($platformRes.Length -eq 0) {
    throw "Unable to find 'ANY' eligible platform container registry!"
}

$acr = ($platformRes | Where-Object { $_.tags.'ard-environment' -eq $DeployEnvironment })
if (!$acr) {
    throw "Unable to find eligible prod container registry!"
}
$AcrName = $acr.Name
$Password = (az acr credential show -n $AcrName | ConvertFrom-Json).passwords[0].value

az container create -g $ResourceGroupName --name demoapp --image "$AcrName.azurecr.io/demoapp:latest" --registry-password $Password --registry-username $AcrName --ip-address Public --ports 80