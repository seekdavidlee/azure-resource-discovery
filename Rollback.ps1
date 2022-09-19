$ErrorActionPreference = "Stop"
$names = az group list --tag ard-internal-solution-id=ard --query "[].name" | ConvertFrom-Json

# Policy assignments are automatically removed when scope i.e. rg is removed.
foreach ($name in $names) {
    Write-Host "Removing resource group $name"
    az group delete --name $name --yes
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to remove resource group."
    }
}

$policies = (az policy definition list --query "[].{Name:name, Id:id}" | ConvertFrom-Json) | Where-Object { $_.Name.StartsWith("ard-") }
$policies | ForEach-Object {
    $name = $_.Name
    Write-Host "Removing policy $name"
    az policy definition delete --name $name
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to remove Policy defination."
    }
}