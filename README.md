# Usage
To use this solution, create a manifest file and run the Apply command.

```
.\Apply.ps1 -ManifestFilePath <Path to>\manifest.json
```

# Problem Statement

As part of a Continuous Deployment process, we will create an Azure environment by defining Azure resources in an ARM Template, Bicep or Terraform (aka Infra-as-code) and then deploy application code into specified Azure resources. To be exact, we would often need to pass the resource names created by the Infra-as-code and inject them as variables into the Azure Pipelines or GitHub Workflow as we are deploying our application code. This creates an unnecessary dependency for the pipeline/workflow related to creating the Azure environment and the pipeline/workflow for deployment of application code.

## Example

Let’s take an example for a customer solution/workload running on Azure. In this example, the solution uses several Azure Resources that includes an AKS cluster, Azure Key Vault for storing secrets, Azure Container Registry for pulling container images, Virtual Network and Subnets, an Azure SQL Database and Storage Account for static contents. We can define these resources as Infra-as-code and include them as part of pipeline or workflow. However, during application deployment, we would require AKS cluster name, Azure Container Registry resource name for pulling container images from, Azure Key Vault secrets as part of application configuration and SQL server and database names. In order to get those resource names, we have to either run the Azure environment creation pipeline or workflow first or pull the resource names from a configuration store (such as variable groups or Azure app configuration service).

Some may argue that these resource names will never change, and why not just store them as configuration somewhere and pull them out. However, it is a bad assumption to say we would never need to recreate said resources again because issues do occur and sometimes we cannot recover from those issues. When that happens and if we need to for example re-create the AKS cluster or change the SQL server or database, the names will change, and we need to remember to update the configurations. Anytime there is human intervention, there is bound to be errors at some point. 

# Solution
This project seeks to eliminate the issue of dependency between the creation of Azure Environment and deployment of application code with the idea that we can self-discover Azure resources at runtime (without the need to update any configurations or even the need to store resource names as configurations). In our problem statement, when a resource is deleted and recreated with another name, you are not impacted because the tool is still using the static tag value. In other words, the goal of ARD for DevOps is simplify code deployment dependencies lookup. 

## High Level Implementation Idea
The tool will return a list Azure resource names related to a customer workload or a solution and those resource names could be used by the pipeline/workflow code or scripts to configure deployment necessary to push the new application code out. As such, we need to associate related Azure resources by a unique Id that groups them together (let's call this ard-solution-id). In addition, we will also apply ard-resource-id which will uniquely identify a single resource. We can associate meta-data with Azure resources by tags. Hence, ARD will push a tag with a key of ard-solution-id and a value to be determined by the customer like a DevOps engineer. Note that this concept of tagging resources isn't new and is used by AKS or Spring Cloud when resources are created there and tagged with specific key/values.

We also need to further return a resource name based on environment such as Dev, Stage and Prod. The same ard-solution-id could be there  but for identifying unique resources in different environments, we need this key of ard-environment and values of dev, stage or prod. 

Another filter could be geography (primary or DR), where we have a resource unique to a region and we need another key of ard-region-id for the purpose of identifying a primary or DR resource. This could be potentially optional filter field but available as part of ARD for DevOps.

Now that we have covered the basics, we need to further refine this idea so that it can work at scale. In a real customer scenario, there will be more than one dev team with shared Azure resources managed by another team such as a Platform team i.e. Azure Container Registry could be shared across multiple dev teams and required as a variable in their individual app specific deployment pipeline/workflow. This means our solution will need to support getting results from multiple ard-solution-id.

## How It Works
During environment (resources) creation, we need to inject the tags we have mentioned. During app code deployment, we will then be able to populate the pipeline or workflow with runtime generated variables that the user can just use that identifies the Azure resource. This is similar to: 

```  az resource list --tag ard-solution-id=foobar ```

This azure cli command will pull all azure resources with tag that matches. We can then use the output to populate as pipeline variables so the pipeline tasks downstream can use said variables aka the Azure resource name.

### Azure Policy

The best way to apply resource tagging would be via Azure Policy. A CLI tool we create whill generate the appropriate Azure Policies for tagging conditions based on either resource group, and/or resource type, and apply the policies on a Subscription. 

### Custom task/action

The best way to populate the pipeline or workflow with variables we have looked up during code deployment is via a custom task or action. Hence, we will create a custom Azure DevOps task (https://docs.microsoft.com/en-us/azure/devops/extend/develop/add-build-task?view=azure-devops) for Azure DevOps and a custom GitHub Action (https://docs.github.com/en/actions/creating-actions/about-custom-actions) for GitHub workflow. 

# Specifications

## Tags
* ard-resource-id
* ard-solution-id
* ard-environment
* ard-region (optional)
