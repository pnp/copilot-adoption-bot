using Azure;
using Azure.Data.Tables;
using Common.Engine.Config;
using Common.Engine.Storage;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Common.Engine;


public abstract class TableStorageManager
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger _logger;
    private ConcurrentDictionary<string, TableClient> _tableClientCache = new();

    public TableStorageManager(StorageAuthConfig storageAuthConfig, ILogger logger)
    {
        _tableServiceClient = AzureStorageClientFactory.CreateTableServiceClient(storageAuthConfig, logger);
        _logger = logger;
    }

    public async Task<TableClient> GetTableClient(string tableName)
    {
        if (_tableClientCache.TryGetValue(tableName, out var tableClient))
        {
            _logger.LogDebug("Retrieved cached table client for {TableName}", tableName);
            return tableClient;
        }

        _logger.LogDebug("Creating new table client for {TableName}", tableName);
        
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
                _logger.LogDebug("Successfully ensured table {TableName} exists", tableName);
                break; // Success, exit retry loop
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableAlreadyExists")
            {
                // Supposedly CreateTableIfNotExistsAsync should silently fail if already exists, but this doesn't seem to happen
                _logger.LogDebug("Table {TableName} already exists", tableName);
                break; // Exit retry loop
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "TableBeingDeleted")
            {
                if (attempt == maxRetries)
                {
                    _logger.LogError(ex, "Table {TableName} is being deleted and did not become available after {MaxRetries} retries", tableName, maxRetries);
                    // Final attempt failed, rethrow with more context
                    throw new InvalidOperationException(
                        $"Table '{tableName}' is being deleted and did not become available after {maxRetries} retry attempts over {(retryDelayMs * (Math.Pow(2, maxRetries) - 1)) / 1000} seconds. " +
                        "This may indicate a naming collision or insufficient wait time for table deletion to complete.", ex);
                }
                
                _logger.LogWarning("Table {TableName} is being deleted, retrying in {DelayMs}ms (attempt {Attempt}/{MaxRetries})", 
                    tableName, retryDelayMs, attempt + 1, maxRetries);
                
                // Wait with exponential backoff before retrying
                await Task.Delay(retryDelayMs);
                retryDelayMs *= 2; // Double the delay for next attempt
            }
        }

        tableClient = _tableServiceClient.GetTableClient(tableName);

        _tableClientCache[tableName] = tableClient;
        _logger.LogInformation("Cached new table client for {TableName}", tableName);

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
            _logger.LogInformation("Deleting table {TableName}", tableName);
            await _tableServiceClient.DeleteTableAsync(tableName);
            _tableClientCache.TryRemove(tableName, out _);
            _logger.LogInformation("Successfully deleted table {TableName}", tableName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Table {TableName} does not exist, nothing to delete", tableName);
            // Table doesn't exist, ignore
        }
    }
}
