# Storage Issues

[← Back to Troubleshooting Guide](../TROUBLESHOOTING.md)

## Error: "Unable to connect to storage account"

**Symptoms:**
- Cannot access templates
- Storage connection errors
- Timeout errors

**Solutions:**

1. **Verify Storage Configuration:**
   ```bash
   # Check storage configuration
   dotnet user-secrets list | grep Storage
   
   # For Connection String (Legacy):
   # ConnectionStrings:Storage = DefaultEndpointsProtocol=https;AccountName=...
   
   # For RBAC:
   # StorageAuthConfig:UseRBAC = true
   # StorageAuthConfig:StorageAccountName = yourstorageaccount
   ```

2. **Check Storage Account:**
   - Go to Azure Portal → Storage Account
   - Verify account exists and is accessible
   - Check if account key is correct

3. **Firewall Rules:**
   - Go to Storage Account → Networking
   - Add your IP address or App Service IPs
   - Ensure firewall allows necessary connections

4. **Test Connectivity:**
   ```bash
   # Use Azure Storage Explorer
   # Try to connect with same connection string or RBAC credentials
   ```

5. **RBAC Authentication Issues (when UseRBAC = true):**
   - Verify RBAC role assignments on the storage account:
     - `Storage Blob Data Contributor`
     - `Storage Table Data Contributor`
     - `Storage Queue Data Contributor`
   - Check identity being used:
     - Local dev: Azure CLI (`az account show`)
     - Azure: Managed Identity or service principal
   - If using override credentials, verify ClientId, ClientSecret, and TenantId are correct
   - Test RBAC permissions:
     ```powershell
     az storage blob list --account-name yourstorageaccount --container-name message-templates --auth-mode login
     ```

## Error: "Blob container not found"

**Symptoms:**
- Templates cannot be saved
- Blob storage errors

**Solutions:**

1. **Container Auto-Creation:**
   - The `message-templates` container is auto-created on first use
   - Check logs for container creation errors

2. **Manual Creation:**
   ```powershell
   az storage container create `
     --name message-templates `
     --connection-string "your-connection-string"
   ```

3. **Permissions:**
   - Ensure storage account has blob service enabled
   - Verify connection string has blob access

## Table Storage Errors

**Symptoms:**
- Cannot save templates
- User cache errors

**Solutions:**

1. **Verify Table Service:**
   - Go to Azure Portal → Storage Account
   - Ensure Table service is enabled

2. **Check Table Names:**
   - messagetemplates
   - messagelogs
   - UserCache
   - SyncMetadata

3. **Test Table Access:**
   ```powershell
   az storage table list `
     --connection-string "your-connection-string"
   ```


## Error: "The specified table is being deleted. Try operation later"

**Symptoms:**
- Status 409 (Conflict)
- ErrorCode: TableBeingDeleted
- Occurs during table creation or access
- Common in integration tests with table cleanup

**Causes:**
- Azure Table Storage deletion is not instantaneous
- Previous test or operation deleted the table
- New operation attempts to create table before deletion completes
- Race condition between table deletion and creation

**Solutions:**

1. **Automatic Retry (Built-in):**
   - The application includes automatic retry logic with exponential backoff
   - Will retry up to 10 times with increasing delays starting at 2 seconds
   - Maximum total wait time: approximately 34 minutes (suitable for cloud environments)
   - Most operations will succeed after brief delay

2. **For Integration Tests:**
   - Tests use highly unique table name prefixes with milliseconds and random components
   - Example: `test{yyyyMMddHHmmssfff}{random}`
   - Implemented in test initialization:
     ```csharp
     var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
     var random = new Random().Next(1000, 9999);
     _testTablePrefix = $"test{timestamp}{random}";
     ```
   - This prevents naming collisions even in parallel test execution

3. **Manual Workaround:**
   - If error persists, wait 30-60 seconds before retrying
   - Azure typically completes table deletion within this timeframe
   - In cloud CI/CD environments (GitHub Actions, Azure DevOps), longer delays may be needed

4. **Production Environments:**
   - Avoid deleting and recreating tables frequently
   - Use table clearing operations instead of delete/create
   - Plan maintenance windows for table schema changes


## Error: Integration tests getting wrong messages from queue

**Symptoms:**
- Test assertions fail with wrong GUID values
- Expected message IDs don't match actual values
- Tests pass locally but fail in CI/CD
- Example: `Assert.AreEqual failed. Expected:<guid1>. Actual:<guid2>`

**Causes:**
- Multiple tests sharing the same Azure Storage Queue
- Parallel test execution causing message interference
- Leftover messages from previous test runs
- Insufficient test isolation

**Solutions:**

1. **Unique Queue Names (Implemented):**
   - Tests now use unique queue names per execution
   - Format: `batchtest{yyyyMMddHHmmssfff}{random}`
   - Example implementation:
     ```csharp
     var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
     var random = new Random().Next(1000, 9999);
     _testQueueName = $"batchtest{timestamp}{random}";
     
     _service = new BatchQueueService(
         GetStorageAuthConfig(),
         GetLogger<BatchQueueService>(),
         _testQueueName
     );
     ```

2. **Test Cleanup:**
   - Tests automatically delete their queues after completion
   - Uses `DeleteQueueAsync()` in `[TestCleanup]`
   - Prevents message accumulation between test runs

3. **Queue Name Parameter:**
   - `BatchQueueService` now accepts optional `queueName` parameter
   - Defaults to `"batch-messages"` for production use
   - Tests provide unique names for isolation
