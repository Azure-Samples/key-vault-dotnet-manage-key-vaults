// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;

namespace ManageKeyVault
{
    public class Program
    {
        /**
         * Azure Key Vault sample for managing key vaults -
         *  - Create a key vault
         *  - Authorize an application
         *  - Update a key vault
         *    - alter configurations
         *    - change permissions
         *  - Create another key vault
         *  - List key vaults
         *  - Delete a key vault.
         */
        private static ResourceIdentifier? _resourceGroupId = null;
        private const int _maxStalenessPrefix = 100000;
        private const int _maxIntervalInSeconds = 300;

        public static async Task RunSample(ArmClient client)
        {
            try
            {
                //============================================================

                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                var rgName = Utilities.CreateRandomName("KeyVaultRG");
                Utilities.Log($"creating resource group with name:{rgName}");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                var resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //Create a KeyVault
                Utilities.Log("Creating a KeyVault...");
                var vaultName1 = Utilities.CreateRandomName("vault1");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var skuName = KeyVaultSkuName.Premium;
                var sku = new KeyVaultSku(KeyVaultSkuFamily.A, skuName);
                var properties = new KeyVaultProperties(Guid.Parse(tenantId), sku);
                var content = new KeyVaultCreateOrUpdateContent(AzureLocation.WestUS, properties);

                // Get the key vaults collection in the resource group
                var collection = resourceGroup.GetKeyVaults();

                // Create or update a key vault using the parameters
                var keyVaultLro = await collection.CreateOrUpdateAsync(WaitUntil.Completed, vaultName1, content);
                var keyVault = keyVaultLro.Value;
                Utilities.Log("Created a KeyVault with name:" + keyVault.Data.Name);

                //============================================================

                // Authorize an application
                Utilities.Log("Authorizing the application associated with the current service principal...");
                var operationKind = AccessPolicyUpdateKind.Add;
                var objectId = Environment.GetEnvironmentVariable("OBJECT_ID");
                var permissions = new IdentityAccessPermissions()
                {
                    Keys = 
                    { 
                        IdentityAccessKeyPermission.All 
                    },
                    Secrets = 
                    { 
                        IdentityAccessSecretPermission.Get, 
                        IdentityAccessSecretPermission.List 
                    }
                };
                var policy = new KeyVaultAccessPolicy(Guid.Parse(tenantId), objectId, permissions);
                var accessPolicies = new List<KeyVaultAccessPolicy>();
                accessPolicies.Add(policy);
                var AccessPolicyPropertie = new KeyVaultAccessPolicyProperties(accessPolicies);
                var keyVaultAccessPolicyParameters = new KeyVaultAccessPolicyParameters(AccessPolicyPropertie);
                _ = await keyVault.UpdateAccessPolicyAsync(operationKind, keyVaultAccessPolicyParameters);
                Utilities.Log("Authorized the application associated with the current service principal...");

                //============================================================

                // Update a key vault
                Utilities.Log("Update a key vault to enable deployments and add permissions to the application...");
                var permissions1 = new IdentityAccessPermissions()
                {
                    Secrets = 
                    { 
                        IdentityAccessSecretPermission.All 
                    },
                };
                var patch = new KeyVaultPatch()
                {
                    Properties = new KeyVaultPatchProperties()
                    {
                        Sku = new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Premium),
                        AccessPolicies = { new KeyVaultAccessPolicy(Guid.Parse(tenantId), objectId, permissions1) },
                        EnabledForDeployment = true,
                        EnabledForTemplateDeployment = true,
                        NetworkRuleSet = new KeyVaultNetworkRuleSet()
                        {
                            Bypass = KeyVaultNetworkRuleBypassOption.AzureServices,
                            DefaultAction = KeyVaultNetworkRuleAction.Allow,
                        },
                        PublicNetworkAccess = "enabled"
                    }
                };
                _ = await keyVault.UpdateAsync(patch);
                Utilities.Log("Updated a KeyVault with name:" + keyVault.Data.Name);

                //============================================================

                // Create another key vault
                var vaultName2 = Utilities.CreateRandomName("vault2");
                var properties2 = new KeyVaultProperties(Guid.Parse(tenantId), sku);
                var content2 = new KeyVaultCreateOrUpdateContent(AzureLocation.EastUS, properties2);
                var keyVaultLro2 = await collection.CreateOrUpdateAsync(WaitUntil.Completed, vaultName2, content2);
                var keyVault2 = keyVaultLro2.Value;
                Utilities.Log("Created another key vault with name:" + keyVault2.Data.Name);

                // Define Access Policy
                var operationKind2 = AccessPolicyUpdateKind.Add;
                var permissions2 = new IdentityAccessPermissions()
                {
                    Keys = 
                    { IdentityAccessKeyPermission.List, 
                        IdentityAccessKeyPermission.Get, 
                        IdentityAccessKeyPermission.Decrypt
                    },
                    Secrets =
                    {
                        IdentityAccessSecretPermission.Get
                    }
                };
                var policy2 = new KeyVaultAccessPolicy(Guid.Parse(tenantId), objectId, permissions2);
                var accessPolicies2 = new List<KeyVaultAccessPolicy>();
                accessPolicies.Add(policy2);
                var accessPolicyProperties2 = new KeyVaultAccessPolicyProperties(accessPolicies2);
                var keyVaultAccessPolicyParameters2 = new KeyVaultAccessPolicyParameters(accessPolicyProperties2);
                _ = await keyVault2.UpdateAccessPolicyAsync(operationKind2, keyVaultAccessPolicyParameters2);
                Utilities.Log("Defined Access Policy");

                //============================================================

                // List key vaults
                Utilities.Log("Listing key vaults...");
                var listByResourceGroup = new List<KeyVaultResource>();
                await foreach (var item in collection.GetAllAsync())
                {
                    listByResourceGroup.Add(item);
                    Utilities.Log("KeyVaultName:" + item.Data.Name);
                }
                Utilities.Log("Listed key vaults"); 

                //============================================================

                // Delete key vaults
                Utilities.Log("Deleting the key vaults");
                _ = await keyVault.DeleteAsync(WaitUntil.Completed);
                _ = await keyVault2.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Deleted the key vaults");
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);
                await RunSample(client);
            }catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}