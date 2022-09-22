param([Parameter(Mandatory = $true)][string]$SpDisplayName, [Parameter(Mandatory = $false)][string]$SpRole = "Contributor")
$ErrorActionPreference = "Stop"

# Should have a single matching service principal
$sp = (az ad sp list --display-name $SpDisplayName --query "[].{name:appDisplayName,id:id}" | ConvertFrom-Json)[0]
if ($LastExitCode -ne 0 -or !$sp) {
    throw "An error has occured. Unable to resolve service principal with display name input."
}

$SpId = $sp.id

$names = az group list --tag ard-internal-solution-id=ard --query "[].name" | ConvertFrom-Json

# Policy assignments are automatically removed when scope i.e. rg is removed.
foreach ($name in $names) {

    $assignments = az role assignment list --assignee $SpId --resource-group $name | ConvertFrom-Json
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to review role assignments for resource group $name."
    }

    if (($assignments | Where-Object { $_.roleDefinitionName -eq $SpRole }).Length -eq 0) {
        Write-Host "Assigning $SpRole role to resource group $name"
        az role assignment create --assignee $SpId --role $SpRole --resource-group $name
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to assign role to resource group."
        }        
    }
    else {
        Write-Host "Existing assignment of $SpRole role in resource group $name found."
    }
}