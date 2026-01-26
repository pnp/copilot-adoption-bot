using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Common.Engine.Config;

namespace Common.Engine.Storage;

/// <summary>
/// Factory for creating Azure Storage clients with support for both connection string and RBAC authentication.
/// Centralizes the logic for creating TableServiceClient, BlobServiceClient, and QueueServiceClient.
/// </summary>
public static class AzureStorageClientFactory
{
    /// <summary>
    /// Creates a TableServiceClient using the provided storage authentication configuration.
    /// </summary>
    /// <param name="storageAuthConfig">Storage authentication configuration</param>
    /// <returns>Configured TableServiceClient instance</returns>
    public static TableServiceClient CreateTableServiceClient(StorageAuthConfig storageAuthConfig)
    {
        if (storageAuthConfig.UseRBAC)
        {
            ValidateRBACConfig(storageAuthConfig);
            var tableEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.table.core.windows.net");
            var credential = GetCredential(storageAuthConfig);
            return new TableServiceClient(tableEndpoint, credential);
        }
        else
        {
            ValidateConnectionString(storageAuthConfig);
            return new TableServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Creates a BlobServiceClient using the provided storage authentication configuration.
    /// </summary>
    /// <param name="storageAuthConfig">Storage authentication configuration</param>
    /// <returns>Configured BlobServiceClient instance</returns>
    public static BlobServiceClient CreateBlobServiceClient(StorageAuthConfig storageAuthConfig)
    {
        if (storageAuthConfig.UseRBAC)
        {
            ValidateRBACConfig(storageAuthConfig);
            var blobEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.blob.core.windows.net");
            var credential = GetCredential(storageAuthConfig);
            return new BlobServiceClient(blobEndpoint, credential);
        }
        else
        {
            ValidateConnectionString(storageAuthConfig);
            return new BlobServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Creates a QueueServiceClient using the provided storage authentication configuration.
    /// </summary>
    /// <param name="storageAuthConfig">Storage authentication configuration</param>
    /// <returns>Configured QueueServiceClient instance</returns>
    public static QueueServiceClient CreateQueueServiceClient(StorageAuthConfig storageAuthConfig)
    {
        if (storageAuthConfig.UseRBAC)
        {
            ValidateRBACConfig(storageAuthConfig);
            var queueEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.queue.core.windows.net");
            var credential = GetCredential(storageAuthConfig);
            return new QueueServiceClient(queueEndpoint, credential);
        }
        else
        {
            ValidateConnectionString(storageAuthConfig);
            return new QueueServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Gets the appropriate Azure credential based on the configuration.
    /// Uses RBACOverrideCredentials if provided, otherwise uses DefaultAzureCredential.
    /// </summary>
    private static Azure.Core.TokenCredential GetCredential(StorageAuthConfig storageAuthConfig)
    {
        if (storageAuthConfig.RBACOverrideCredentials != null)
        {
            return new ClientSecretCredential(
                storageAuthConfig.RBACOverrideCredentials.TenantId,
                storageAuthConfig.RBACOverrideCredentials.ClientId,
                storageAuthConfig.RBACOverrideCredentials.ClientSecret);
        }

        // Use DefaultAzureCredential (Managed Identity, Azure CLI, Environment Variables, etc.)
        return new DefaultAzureCredential();
    }

    /// <summary>
    /// Validates that the storage account name is provided when using RBAC.
    /// </summary>
    private static void ValidateRBACConfig(StorageAuthConfig storageAuthConfig)
    {
        if (string.IsNullOrEmpty(storageAuthConfig.StorageAccountName))
        {
            throw new InvalidOperationException("StorageAccountName is required when UseRBAC is true");
        }
    }

    /// <summary>
    /// Validates that the connection string is provided when not using RBAC.
    /// </summary>
    private static void ValidateConnectionString(StorageAuthConfig storageAuthConfig)
    {
        if (string.IsNullOrEmpty(storageAuthConfig.ConnectionString))
        {
            throw new InvalidOperationException("ConnectionString is required when UseRBAC is false");
        }
    }
}
