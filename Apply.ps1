# Dev Test is used when we are developing azure-resource-discovery and used to update the internal solution id.

param([Parameter(Mandatory = $true)][string]$ManifestFilePath, [Switch]$DevTest)
$ErrorActionPreference = "Stop"

if ($DevTest) {
    $internalSolutionId = "arddevtest"
}
else {
    $internalSolutionId = "ard"
}

if (!(Test-Path $ManifestFilePath)) {
    throw "Invalid manifest file path $ManifestFilePath"
}

$resultsFile = "results.json"

Push-Location .\AzureResourceDiscoveryCli\AzureResourceDiscovery

$groups = az group list --query "[].{Name:name, Id:id}" | ConvertFrom-Json
if ($LastExitCode -ne 0) {
    Pop-Location
    throw "An error has occured. Unable to query for resource groups."
}

$customerManifest = Get-Content $ManifestFilePath | ConvertFrom-Json

$rgTagging = $customerManifest."group-resources"

# Policy assignment requires the use of a managed identity.
$mi = $customerManifest."managed-identity"
$miResourceGroup = $mi."resource-group-name"
$miName = $mi."name"

$exist = $groups | Where-Object { $_.Name -eq $miResourceGroup }
if (!$exist) {

    # if group does not exist, create it
    Write-Host "Managed identity resource group $miResourceGroup does not exist. Creating it now..."
    az group create --name $miResourceGroup --location $customerManifest."resource-group-location" --tags ard-internal-solution-id=$internalSolutionId | ConvertFrom-Json
    if ($LastExitCode -ne 0) {
        Pop-Location
        throw "An error has occured. Unable to create resource group $miResourceGroup."
    }

    # lock the resource group
    az lock create --name "$miResourceGroup-lock" --lock-type CanNotDelete --resource-group $miResourceGroup
    if ($LastExitCode -ne 0) {
        Pop-Location
        throw "An error has occured. Unable to lock resource group $miResourceGroup."
    }
}
else {
    Write-Host "Group $miResourceGroup exist"
}

$ids = az identity list -g $miResourceGroup | ConvertFrom-Json
if ($LastExitCode -ne 0) {
    Pop-Location
    throw "An error has occured. Unable to query for managed identity in resource group $miResourceGroup."
}

if ($ids.Length -eq 0 -or ($ids | Where-Object { $_.name -eq $miName }).Length -ne 1) {
    # Create managed identity in resource group
    $mid = az identity create --name $miName --resource-group $miResourceGroup --tags ard-internal-solution-id=$internalSolutionId | ConvertFrom-Json
    if ($LastExitCode -ne 0) {
        Pop-Location
        throw "An error has occured. Unable to create managed identity."
    } 
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
    Pop-Location
    throw "An error has occured. Unable to get subscription Id."
}
$taggingRoleId = "/subscriptions/$subId/providers/Microsoft.Authorization/roleDefinitions/4a9ae827-6dc8-4573-8ac7-8239d42aa03f"


foreach ($item in $manifest.Items) {
    
    $name = "$internalSolutionId-" + $item.Name
    $displayName = $item.DisplayName
    $description = $item.Description
    $filePath = $item.FilePath

    Write-Output "Processing $filePath"

    az policy definition create --name $name --display-name  $displayName --description $description --rules $filePath --mode All
    if ($LastExitCode -ne 0) {
        Pop-Location
        throw "An error has occured. Unable to create policy defination $name."
    }
    $resourceGroupNames = $item.ResourceGroupNames

    foreach ($resourceGroupName in $resourceGroupNames) {

        # Does rg exist in sub?
        $exist = $groups | Where-Object { $_.Name -eq $resourceGroupName }
        if (!$exist) {

            $find = $rgTagging | Where-Object { $_."resource-group-names".Contains($resourceGroupName) }

            # if group does not exist, create it
            Write-Host "Group $resourceGroupName does not exist. Creating it now..."
            if ($find) {

                Write-Host "Ensuring tags exist for ard-*"

                $ardSolutionId = $find."ard-solution-id"
                $ardEnvironment = $find."ard-environment"
                $newGroup = az group create --name $resourceGroupName --location $manifest.ResourceGroupLocation `
                    --tags ard-internal-solution-id=$internalSolutionId ard-solution-id=$ardSolutionId ard-environment=$ardEnvironment | ConvertFrom-Json
            }
            else {
                $newGroup = az group create --name $resourceGroupName --location $manifest.ResourceGroupLocation `
                    --tags ard-internal-solution-id=$internalSolutionId  | ConvertFrom-Json
            }

            if ($LastExitCode -ne 0) {
                Pop-Location
                throw "An error has occured. Unable to create resource group $resourceGroupName."
            }
            $groups += @{ Name = $newGroup.name; Id = $newGroup.id }

            az lock create --name "$resourceGroupName-lock" --lock-type CanNotDelete --resource-group $resourceGroupName
            if ($LastExitCode -ne 0) {
                Pop-Location
                throw "An error has occured. Unable to lock resource group $resourceGroupName."
            }
        }
        else {
            Write-Host "Group $resourceGroupName exist"

            $find = $rgTagging | Where-Object { $_."resource-group-names".Contains($resourceGroupName) }
            if ($find) {
                
                Write-Host "Ensuring tags exist for ard-*"
                
                $ardSolutionId = $find."ard-solution-id"
                $ardEnvironment = $find."ard-environment"

                az group update --name $resourceGroupName `
                    --tags ard-internal-solution-id=$internalSolutionId ard-solution-id=$ardSolutionId ard-environment=$ardEnvironment 
            }
        }

        # Perform role assignment
        az role assignment create --assignee-object-id $mid.principalId --role $taggingRoleId `
            --resource-group $resourceGroupName --assignee-principal-type ServicePrincipal
        if ($LastExitCode -ne 0) {
            Pop-Location
            throw "An error has occured. Unable to create managed identity in resource group $resourceGroupName."
        }

        $policyId = "/subscriptions/$subId/providers/Microsoft.Authorization/policyDefinitions/$name"        
        $scope = "/subscriptions/$subId/resourceGroups/$resourceGroupName"
        az policy assignment create --display-name "$displayName $resourceGroupName" --name $name --scope $scope --policy $policyId `
            --mi-user-assigned $mid.id --location $mid.location
            
        if ($LastExitCode -ne 0) {
            Pop-Location
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