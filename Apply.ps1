param([Parameter(Mandatory = $true)][string]$ManifestFilePath)
$ErrorActionPreference = "Stop"

if (!(Test-Path $ManifestFilePath)) {
    throw "Invalid manifest file path $ManifestFilePath"
}

$resultsFile = "results.json"

Push-Location .\AzureResourceDiscoveryCli\AzureResourceDiscovery

$groups = az group list --query "[].{Name:name, Id:id}" | ConvertFrom-Json
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to query for resource groups."
}

$customerManifest = Get-Content $ManifestFilePath | ConvertFrom-Json

# Policy assignment requires the use of a managed identity.
$mi = $customerManifest."managed-identity"
$miResourceGroup = $mi."resource-group-name"
$miName = $mi."name"

$exist = $groups | Where-Object { $_.Name -eq $miResourceGroup }
if (!$exist) {

    # if group does not exist, create it
    Write-Host "Managed identity resource group $miResourceGroup does not exist. Creating it now..."
    az group create --name $miResourceGroup --location $customerManifest."resource-group-location" --tags ard-internal-solution-id=ard | ConvertFrom-Json
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to create resource group $miResourceGroup."
    }
}
else {
    Write-Host "Group $miResourceGroup exist"
}

$ids = az identity list -g $miResourceGroup | ConvertFrom-Json
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to query for managed identity in resource group $miResourceGroup."
}

if ($ids.Length -eq 0 -or ($ids | Where-Object { $_.name -eq $miName }).Length -ne 1) {
    # Create managed identity in resource group
    $mid = az identity create --name $miName --resource-group $miResourceGroup --tags ard-internal-solution-id=ard | ConvertFrom-Json    
}
else {
    $mid = $ids[0]
}

dotnet run -- -o $resultsFile -d .\ -f $ManifestFilePath
if ($LastExitCode -ne 0) {
    Pop-Location
    throw "An error has occured. Unable to generate Azure policy file(s)."
}
$manifest = Get-Content $resultsFile | ConvertFrom-Json

# Script requires these variables
$subId = az account show --query id --output tsv
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to get subscription Id."
}
$taggingRoleId = "/subscriptions/$subId/providers/Microsoft.Authorization/roleDefinitions/4a9ae827-6dc8-4573-8ac7-8239d42aa03f"


foreach ($item in $manifest.Items) {
    
    $name = "ard-" + $item.Name
    $displayName = $item.DisplayName
    $description = $item.Description
    $filePath = $item.FilePath

    Write-Output "Processing $filePath"

    az policy definition create --name $name --display-name  $displayName --description $description --rules $filePath --mode All
    if ($LastExitCode -ne 0) {
        throw "An error has occured. Unable to create policy defination $name."
    }
    $resourceGroupNames = $item.ResourceGroupNames

    foreach ($resourceGroupName in $resourceGroupNames) {

        # Does rg exist in sub?
        $exist = $groups | Where-Object { $_.Name -eq $resourceGroupName }
        if (!$exist) {

            # if group does not exist, create it
            Write-Host "Group $resourceGroupName does not exist. Creating it now..."
            $newGroup = az group create --name $resourceGroupName --location $manifest.ResourceGroupLocation `
                --tags ard-internal-solution-id=ard | ConvertFrom-Json
            if ($LastExitCode -ne 0) {
                throw "An error has occured. Unable to create resource group $resourceGroupName."
            }
            $groups += @{ Name = $newGroup.name; Id = $newGroup.id }
        }
        else {
            Write-Host "Group $resourceGroupName exist"
        }

        # Perform role assignment
        az role assignment create --assignee-object-id $mid.principalId --role $taggingRoleId `
            --resource-group $resourceGroupName --assignee-principal-type ServicePrincipal
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to create managed identity in resource group $resourceGroupName."
        }

        $policyId = "/subscriptions/$subId/providers/Microsoft.Authorization/policyDefinitions/$name"
        $assignmentName = "$name"
        $scope = "/subscriptions/$subId/resourceGroups/$resourceGroupName"
        az policy assignment create --name $assignmentName  --scope $scope --policy $policyId `
            --mi-user-assigned $mid.id --location $mid.location
            
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to perform policy asssingment in scope of $resourceGroupName."
        }
    }

    Remove-Item $filePath -Force
    if (!$rootDir) {
        $rootDir = $filePath.Split('\')[1]
    }
}

Remove-Item $rootDir -Force
Remove-Item $resultsFile -Force
Pop-Location