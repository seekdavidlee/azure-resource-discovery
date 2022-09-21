param($AcrName)

# Login to ACR
az acr login --name $AcrName --expose-token
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to login to acr."
}

Push-Location DemoWebApp
az acr build --image demoapp -r $AcrName --file ./DemoWebApp/Dockerfile .
Pop-Location