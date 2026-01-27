using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Common.Engine.Config;
using Microsoft.Extensions.Logging;

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
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>Configured TableServiceClient instance</returns>
    public static TableServiceClient CreateTableServiceClient(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (storageAuthConfig.UseRBAC)
        {
            logger.LogDebug("Creating TableServiceClient using RBAC authentication for storage account {StorageAccountName}", storageAuthConfig.StorageAccountName);
            ValidateRBACConfig(storageAuthConfig, logger);
            var tableEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.table.core.windows.net");
            var credential = GetCredential(storageAuthConfig, logger);
            logger.LogInformation("Successfully created TableServiceClient using RBAC for {StorageAccountName}", storageAuthConfig.StorageAccountName);
            return new TableServiceClient(tableEndpoint, credential);
        }
        else
        {
            logger.LogDebug("Creating TableServiceClient using connection string authentication");
            ValidateConnectionString(storageAuthConfig, logger);
            logger.LogInformation("Successfully created TableServiceClient using connection string");
            return new TableServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Creates a BlobServiceClient using the provided storage authentication configuration.
    /// </summary>
    /// <param name="storageAuthConfig">Storage authentication configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>Configured BlobServiceClient instance</returns>
    public static BlobServiceClient CreateBlobServiceClient(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (storageAuthConfig.UseRBAC)
        {
            logger.LogDebug("Creating BlobServiceClient using RBAC authentication for storage account {StorageAccountName}", storageAuthConfig.StorageAccountName);
            ValidateRBACConfig(storageAuthConfig, logger);
            var blobEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.blob.core.windows.net");
            var credential = GetCredential(storageAuthConfig, logger);
            logger.LogInformation("Successfully created BlobServiceClient using RBAC for {StorageAccountName}", storageAuthConfig.StorageAccountName);
            return new BlobServiceClient(blobEndpoint, credential);
        }
        else
        {
            logger.LogDebug("Creating BlobServiceClient using connection string authentication");
            ValidateConnectionString(storageAuthConfig, logger);
            logger.LogInformation("Successfully created BlobServiceClient using connection string");
            return new BlobServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Creates a QueueServiceClient using the provided storage authentication configuration.
    /// </summary>
    /// <param name="storageAuthConfig">Storage authentication configuration</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <returns>Configured QueueServiceClient instance</returns>
    public static QueueServiceClient CreateQueueServiceClient(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (storageAuthConfig.UseRBAC)
        {
            logger.LogDebug("Creating QueueServiceClient using RBAC authentication for storage account {StorageAccountName}", storageAuthConfig.StorageAccountName);
            ValidateRBACConfig(storageAuthConfig, logger);
            var queueEndpoint = new Uri($"https://{storageAuthConfig.StorageAccountName}.queue.core.windows.net");
            var credential = GetCredential(storageAuthConfig, logger);
            logger.LogInformation("Successfully created QueueServiceClient using RBAC for {StorageAccountName}", storageAuthConfig.StorageAccountName);
            return new QueueServiceClient(queueEndpoint, credential);
        }
        else
        {
            logger.LogDebug("Creating QueueServiceClient using connection string authentication");
            ValidateConnectionString(storageAuthConfig, logger);
            logger.LogInformation("Successfully created QueueServiceClient using connection string");
            return new QueueServiceClient(storageAuthConfig.ConnectionString);
        }
    }

    /// <summary>
    /// Gets the appropriate Azure credential based on the configuration.
    /// Uses RBACOverrideCredentials if provided, otherwise uses DefaultAzureCredential.
    /// </summary>
    private static Azure.Core.TokenCredential GetCredential(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (storageAuthConfig.RBACOverrideCredentials != null)
        {
            logger.LogDebug("Using ClientSecretCredential with override credentials for tenant {TenantId}", 
                storageAuthConfig.RBACOverrideCredentials.TenantId);
            return new ClientSecretCredential(
                storageAuthConfig.RBACOverrideCredentials.TenantId,
                storageAuthConfig.RBACOverrideCredentials.ClientId,
                storageAuthConfig.RBACOverrideCredentials.ClientSecret);
        }

        logger.LogDebug("Using DefaultAzureCredential (Managed Identity, Azure CLI, Environment Variables, etc.)");
        // Use DefaultAzureCredential (Managed Identity, Azure CLI, Environment Variables, etc.)
        return new DefaultAzureCredential();
    }

    /// <summary>
    /// Validates that the storage account name is provided when using RBAC.
    /// </summary>
    private static void ValidateRBACConfig(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (string.IsNullOrEmpty(storageAuthConfig.StorageAccountName))
        {
            logger.LogError("StorageAccountName is required when UseRBAC is true but was not provided");
            throw new InvalidOperationException("StorageAccountName is required when UseRBAC is true");
        }
    }

    /// <summary>
    /// Validates that the connection string is provided when not using RBAC.
    /// </summary>
    private static void ValidateConnectionString(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        if (string.IsNullOrEmpty(storageAuthConfig.ConnectionString))
        {
            logger.LogError("ConnectionString is required when UseRBAC is false but was not provided");
            throw new InvalidOperationException("ConnectionString is required when UseRBAC is false");
        }
    }
}
