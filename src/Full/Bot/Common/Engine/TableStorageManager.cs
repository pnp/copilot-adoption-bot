using Azure;
using Azure.Data.Tables;
using Common.Engine.Config;
using Common.Engine.Storage;
using System.Collections.Concurrent;

namespace Common.Engine;


public abstract class TableStorageManager
{
    private readonly TableServiceClient _tableServiceClient;
    private ConcurrentDictionary<string, TableClient> _tableClientCache = new();

    public TableStorageManager(StorageAuthConfig storageAuthConfig)
    {
        _tableServiceClient = AzureStorageClientFactory.CreateTableServiceClient(storageAuthConfig);
    }

    public async Task<TableClient> GetTableClient(string tableName)
    {
        if (_tableClientCache.TryGetValue(tableName, out var tableClient))
            return tableClient;

        // Retry logic for table creation with exponential backoff
        // This handles the case where a table is being deleted and we need to wait
        // Cloud environments may need longer delays than local development
        int maxRetries = 10;
        int retryDelayMs = 2000; // Start with 2 seconds for cloud environments
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _tableServiceClient.CreateTableIfNotExistsAsync(tableName);
                break; // Success, exit retry loop
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableAlreadyExists")
            {
                // Supposedly CreateTableIfNotExistsAsync should silently fail if already exists, but this doesn't seem to happen
                break; // Exit retry loop
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableBeingDeleted")
            {
                if (attempt == maxRetries)
                {
                    // Final attempt failed, rethrow with more context
                    throw new InvalidOperationException(
                        $"Table '{tableName}' is being deleted and did not become available after {maxRetries} retry attempts over {(retryDelayMs * (Math.Pow(2, maxRetries) - 1)) / 1000} seconds. " +
                        "This may indicate a naming collision or insufficient wait time for table deletion to complete.", ex);
                }
                
                // Wait with exponential backoff before retrying
                await Task.Delay(retryDelayMs);
                retryDelayMs *= 2; // Double the delay for next attempt
            }
        }

        tableClient = _tableServiceClient.GetTableClient(tableName);

        _tableClientCache[tableName] = tableClient;

        return tableClient;
    }

    /// <summary>
    /// Delete a table from Azure Table Storage. Used primarily for test cleanup.
    /// </summary>
    /// <param name="tableName">Name of the table to delete</param>
    public async Task DeleteTable(string tableName)
    {
        try
        {
            await _tableServiceClient.DeleteTableAsync(tableName);
            _tableClientCache.TryRemove(tableName, out _);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist, ignore
        }
    }
}
