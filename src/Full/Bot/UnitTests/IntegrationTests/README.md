# Integration Tests for Common.Engine

This directory contains comprehensive integration tests for the Common.Engine project. These tests verify the functionality of major services and storage managers against real Azure resources.

## Test Structure

### Test Classes

1. **MessageTemplateServiceIntegrationTests.cs**
   - Tests for `MessageTemplateService`
   - Covers template CRUD operations
   - Tests batch creation and management
   - Verifies message logging functionality
   - Tests queue integration

2. **BatchQueueServiceIntegrationTests.cs**
   - Tests for `BatchQueueService`
   - Verifies queue operations (enqueue, dequeue, delete)
   - Tests batch message handling
   - Validates queue length tracking

3. **SmartGroupServiceIntegrationTests.cs**
   - Tests for `SmartGroupService`
   - Covers smart group CRUD operations
   - Tests resolution functionality (requires AI service)
   - Verifies caching behavior

4. **GraphUserServiceIntegrationTests.cs**
   - Tests for `GraphUserService`
   - Validates Microsoft Graph integration
   - Tests user retrieval with metadata
   - Verifies department and manager enrichment

5. **StatisticsServiceIntegrationTests.cs**
   - Tests for `StatisticsService`
   - Validates message status statistics
   - Tests user coverage calculations
   - Verifies stat updates after operations

6. **StorageManagerIntegrationTests.cs**
   - Direct tests for storage managers
   - Tests `MessageTemplateStorageManager`
   - Tests `SmartGroupStorageManager`
   - Verifies table operations and data persistence

## Prerequisites

### Required Configuration

These tests require actual Azure resources and valid configuration. Add the following to your user secrets:

```json
{
  "ConnectionStrings": {
    "Storage": "DefaultEndpointsProtocol=https;AccountName=<your-storage-account>;AccountKey=<your-key>"
  },
  "GraphConfig": {
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "ClientSecret": "<your-client-secret>"
  }
}
```

To configure user secrets:
```bash
cd UnitTests
dotnet user-secrets set "ConnectionStrings:Storage" "your-connection-string"
dotnet user-secrets set "GraphConfig:TenantId" "your-tenant-id"
dotnet user-secrets set "GraphConfig:ClientId" "your-client-id"
dotnet user-secrets set "GraphConfig:ClientSecret" "your-client-secret"
```

### Azure Resources Required

1. **Azure Storage Account**
   - Used for Table Storage (templates, batches, logs, smart groups)
   - Used for Queue Storage (batch message processing)
   - Used for Blob Storage (template JSON storage)

2. **Microsoft Graph API Access**
   - Application permissions required:
     - `User.Read.All`
     - `Directory.Read.All`
   - Used for user enumeration and metadata retrieval

## Running the Tests

### Run All Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~MessageTemplateServiceIntegrationTests"
```

### Run Individual Test
```bash
dotnet test --filter "FullyQualifiedName~MessageTemplateServiceIntegrationTests.CreateAndGetTemplate_Success"
```

## Test Categories

### Storage Tests
Tests that directly interact with Azure Table Storage and Blob Storage:
- `StorageManagerIntegrationTests`
- Template and batch CRUD operations
- Message log operations
- Smart group storage operations

### Queue Tests
Tests that interact with Azure Queue Storage:
- `BatchQueueServiceIntegrationTests`
- Message enqueuing and dequeuing
- Queue length management

### Service Layer Tests
Tests that verify service orchestration and business logic:
- `MessageTemplateServiceIntegrationTests`
- `SmartGroupServiceIntegrationTests`
- `StatisticsServiceIntegrationTests`

### External Integration Tests
Tests that require external service connectivity:
- `GraphUserServiceIntegrationTests` (Microsoft Graph)
- Smart group resolution tests (requires AI Foundry service)

## Important Notes

### Data Cleanup
- All tests include cleanup logic in their teardown
- Tests create unique identifiers to avoid conflicts
- Failed tests may leave orphaned data that needs manual cleanup

### Test Isolation
- Tests are designed to be independent and can run in parallel
- Each test creates its own test data with unique identifiers
- Tests clean up after themselves, but failures may leave artifacts

### Performance Considerations
- Integration tests are slower than unit tests due to external dependencies
- Tests involving Microsoft Graph may take several seconds
- Queue operations may have delays due to Azure Queue processing time
- Consider using test delays (`Task.Delay`) when testing eventual consistency

### Limitations

1. **AI Service Tests**
   - Tests requiring `AIFoundryService` will fail if AI is not configured
   - These tests verify that proper exceptions are thrown when AI is unavailable
   - To test AI functionality, configure AI Foundry credentials

2. **Microsoft Graph Tests**
   - Require valid Azure AD tenant with users
   - May fail if app registration lacks required permissions
   - Some tests may be inconclusive if test conditions aren't met (e.g., no users with departments)

3. **Storage Costs**
   - Tests create and delete storage resources
   - Minimal cost impact but consider transaction charges
   - Use development/test storage accounts, not production

## Troubleshooting

### Common Issues

1. **"Storage account not found" errors**
   - Verify connection string in user secrets
   - Ensure storage account exists and is accessible

2. **Graph API permission errors**
   - Verify app registration has required permissions
   - Ensure admin consent has been granted
   - Check tenant ID, client ID, and client secret

3. **Tests timing out**
   - Increase test timeout values if needed
   - Check network connectivity to Azure
   - Verify Azure service availability

4. **Orphaned test data**
   - Manually clean up storage tables if tests fail
   - Look for entities with "Test" prefixes in names
   - Clear queue messages if needed

## Best Practices

1. **Use Test Isolation**
   - Always use unique identifiers (GUIDs) for test data
   - Don't rely on specific data existing in storage
   - Clean up in test cleanup methods

2. **Handle Async Properly**
   - Always await async operations
   - Use appropriate delays for eventual consistency
   - Don't use `.Result` or `.Wait()` - use `await`

3. **Log Appropriately**
   - Use the logger to output test progress
   - Log important values for debugging
   - Help future developers understand test failures

4. **Test Meaningful Scenarios**
   - Test both success and failure paths
   - Verify edge cases (empty lists, null values, etc.)
   - Test business logic, not just CRUD operations

## Future Enhancements

Potential areas for additional integration tests:

1. **Bot Integration Tests**
   - Test conversation flow
   - Test adaptive card rendering
   - Test message sending to Teams

2. **Background Service Tests**
   - Test batch message processor
   - Test default template initialization
   - Test scheduled jobs

3. **End-to-End Scenarios**
   - Complete message send workflow
   - Smart group resolution and messaging
   - Statistics generation and accuracy

4. **Performance Tests**
   - Batch processing performance
   - Large user set handling
   - Concurrent operation handling

5. **Error Recovery Tests**
   - Storage retry logic
   - Queue poison message handling
   - Graph API throttling response
