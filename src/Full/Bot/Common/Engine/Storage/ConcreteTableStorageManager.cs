using Azure;
using Azure.Data.Tables;
using Common.Engine;
using Common.Engine.Config;
using System.Collections.Concurrent;

namespace Common.Engine.Storage;

/// <summary>
/// Concrete implementation of TableStorageManager for general use.
/// </summary>
public class ConcreteTableStorageManager : TableStorageManager
{
    /// <summary>
    /// Legacy constructor using connection string authentication
    /// </summary>
    public ConcreteTableStorageManager(string storageConnectionString) 
        : base(storageConnectionString)
    {
    }

    /// <summary>
    /// Constructor supporting both connection string and RBAC authentication
    /// </summary>
    public ConcreteTableStorageManager(StorageAuthConfig storageAuthConfig)
        : base(storageAuthConfig)
    {
    }
}
