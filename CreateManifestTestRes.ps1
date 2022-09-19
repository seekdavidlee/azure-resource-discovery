# This script is used for testing the output of manifest-test.json
# Be sure to reference manifest-test.json and ensure consistency in rg name and location if you intend to change this script.
# This script is NOT idempotent!!!

$location = "centralus"
$devRg = "test-shared-services-dev"
$name = "test" + (New-Guid).ToString("N").Substring(0, 7)

# Notice the lack of ard specific tag in all resources created which is the main point of showing the policy remediation
az keyvault create --name "$name-dev" --resource-group $devRg --location $location  
az keyvault create --name "$name-prod" --resource-group "test-shared-services-prod" --location $location

az storage account create `
    --name $name `
    --resource-group $devRg `
    --location $location `
    --sku Standard_LRS `
    --kind StorageV2