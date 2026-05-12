using Engine.Config;
using Microsoft.Extensions.Logging;

namespace Engine.Storage;

/// <summary>
/// Concrete implementation of TableStorageManager for general use.
/// </summary>
public class ConcreteTableStorageManager : TableStorageManager
{
    public ConcreteTableStorageManager(StorageAuthConfig storageAuthConfig, ILogger logger)
        : base(storageAuthConfig, logger)
    {
    }
}
