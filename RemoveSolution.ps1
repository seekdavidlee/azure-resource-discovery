param(
    [Parameter(Mandatory = $true)][string]$ArdSolutionId,
    [Parameter(Mandatory = $false)][string]$ArdEnvironment)

$ErrorActionPreference = "Stop"
if (!$ArdEnvironment) {
    $ArdEnvironment = "dev"
}
$groups = az group list --tag ard-environment=$ArdEnvironment | ConvertFrom-Json
$resourceGroupName = ($groups | Where-Object { $_.tags.'ard-solution-id' -eq $ArdSolutionId -and $_.tags.'ard-environment' -eq $ArdEnvironment -and !$_.tags.'aks-managed-cluster-rg' }).name

$count = 0
$ardRes = (az resource list --tag ard-solution-id=$ArdSolutionId | ConvertFrom-Json)
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to list resources for $ArdSolutionId."
}

$devRes = $ardRes | Where-Object { $_.tags.'ard-environment' -eq $ArdEnvironment }
if ($devRes -and $devRes.Length -gt 0) {

    $locks = az lock list --resource-group $resourceGroupName | ConvertFrom-Json
    if ($locks.Length -eq 1) {
        az lock delete --name $locks.name --resource-group $locks.resourceGroup
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to delete lock."
        }
    }

    $retryList = @()

    $devRes | ForEach-Object {
        if ($_.resourceGroup -eq $resourceGroupName) {
            $id = $_.id
            Write-Host "Removing $id"
            az resource delete --id $_.id
            if ($LastExitCode -eq 0) {
                $count += 1
            }
            else {
                $retryList += $_.id
            }
        }    
    }

    if ($retryList.Length -gt 0) {
        foreach ($retryId in $retryList) {
            Write-Host "Retry removing $retryId"
            az resource delete --id $retryId
            if ($LastExitCode -eq 0) {
                $count += 1
            }
        }
    }

    if ($locks.Length -eq 1) {
        az lock create --name $locks.name --lock-type $locks.level --resource-group $locks.resourceGroup
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to re-add lock."
        }
    }
}

Write-Host "Number of resource deleted: $count"