using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Common.Engine;
using System.Collections.Concurrent;

namespace Common.Engine.Storage;

/// <summary>
/// Concrete implementation of TableStorageManager for general use.
/// </summary>
public class ConcreteTableStorageManager : TableStorageManager
{
    public ConcreteTableStorageManager(string storageConnectionString) 
        : base(storageConnectionString)
    {
    }

    public ConcreteTableStorageManager(Uri tableServiceUri, TokenCredential credential)
        : base(tableServiceUri, credential)
    {
    }
}
