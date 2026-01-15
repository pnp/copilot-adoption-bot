using Azure;
using Azure.Data.Tables;
using System.Collections.Concurrent;

namespace Common.Engine;


public abstract class TableStorageManager
{
    private readonly TableServiceClient _tableServiceClient;
    private ConcurrentDictionary<string, TableClient> _tableClientCache = new();
    public TableStorageManager(string storageConnectionString)
    {
        _tableServiceClient = new TableServiceClient(storageConnectionString);
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
