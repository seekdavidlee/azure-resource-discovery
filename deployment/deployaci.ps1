param($ResourceGroupName, $AcrName)

$Password = (az acr credential show -n $AcrName | ConvertFrom-Json).passwords[0].value

az container create -g $ResourceGroupName --name demoapp --image "$AcrName.azurecr.io/demoapp:latest" --registry-password $Password --registry-username $AcrName --ip-address Public --ports 80