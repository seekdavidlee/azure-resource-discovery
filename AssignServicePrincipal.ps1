param([Parameter(Mandatory = $true)][string]$SpId)
$ErrorActionPreference = "Stop"
$names = az group list --tag ard-internal-solution-id=ard --query "[].name" | ConvertFrom-Json

$roleName = "Contributor"
# Policy assignments are automatically removed when scope i.e. rg is removed.
foreach ($name in $names) {

    Write-Host "Assigning $roleName role to resource group $name"
    az role assignment create --assignee $SpId --role $roleName --resource-group $name
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to assign role to resource group."
    }
}