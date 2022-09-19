$ErrorActionPreference = "Stop"

$resultsFile = "results.json"

Push-Location .\AzureResourceDiscoveryCli\AzureResourceDiscovery
dotnet run -- -o $resultsFile -d .\ -f ..\..\manifest.json
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
$identityName = "tag-policy-identity"

$groups = az group list --query "[].{Name:name, Id:id}"
if ($LastExitCode -ne 0) {
    throw "An error has occured. Unable to query for resource groups."
}

foreach ($item in $manifest.Items) {
    
    $name = $item.Name
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
        if ($exist.Length -eq 0) {

            # if group does not exist, create it
            Write-Host "Group $resourceGroupName does not exit. Creating it now..."
            $newGroup = az group create --name $resourceGroupName --location $manifest.ResourceGroupLocation | ConvertFrom-Json
            if ($LastExitCode -ne 0) {
                throw "An error has occured. Unable to create resource group $resourceGroupName."
            }
            $groups += @{ Name = $newGroup.name; Id = $newGroup.id }
        }

        # Policy assignment requires the use of a managed identity.
        $ids = az identity list -g $resourceGroupName | ConvertFrom-Json
        if ($LastExitCode -ne 0) {
            throw "An error has occured. Unable to query for managed identity in resource group $resourceGroupName."
        }

        if ($ids.Length -eq 0 -or ($ids | Where-Object { $_.name -eq $identityName }).Length -ne 1) {

            # Create managed identity in resource group
            $id = az identity create --name $identityName --resource-group $resourceGroupName | ConvertFrom-Json    
            az role assignment create --assignee-object-id $id.principalId --role $taggingRoleId `
                --resource-group $resourceGroupName --assignee-principal-type ServicePrincipal
            if ($LastExitCode -ne 0) {
                throw "An error has occured. Unable to create managed identity in resource group $resourceGroupName."
            }
        }
        else {
            $id = $ids[0]
        }

        $policyId = "/subscriptions/$subId/providers/Microsoft.Authorization/policyDefinitions/$name"
        $assignmentName = "$name-assignment"
        $scope = "/subscriptions/$subId/resourceGroups/$resourceGroupName"
        az policy assignment create --name $assignmentName  --scope $scope --policy $policyId `
            --mi-user-assigned $id.id --location $id.location
            
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