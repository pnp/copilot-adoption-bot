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

    /// <summary>
    /// Legacy constructor using connection string authentication
    /// </summary>
    public TableStorageManager(string storageConnectionString)
    {
        _tableServiceClient = new TableServiceClient(storageConnectionString);
    }

    /// <summary>
    /// Constructor supporting both connection string and RBAC authentication
    /// </summary>
    public TableStorageManager(StorageAuthConfig storageAuthConfig)
    {
        _tableServiceClient = AzureStorageClientFactory.CreateTableServiceClient(storageAuthConfig);
    }

    public async Task<TableClient> GetTableClient(string tableName)
    {
        if (_tableClientCache.TryGetValue(tableName, out var tableClient))
            return tableClient;

        try
        {
            await _tableServiceClient.CreateTableIfNotExistsAsync(tableName);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "TableAlreadyExists")
        {
            // Supposedly CreateTableIfNotExistsAsync should silently fail if already exists, but this doesn't seem to happen
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
