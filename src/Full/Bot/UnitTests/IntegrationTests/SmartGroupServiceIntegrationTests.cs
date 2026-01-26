using Common.Engine;
using Common.Engine.Config;
using Common.Engine.Services;
using Common.Engine.Services.UserCache;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Azure.Identity;
using Microsoft.Graph;
using UnitTests.Fakes;

namespace UnitTests.IntegrationTests;

[TestClass]
public class SmartGroupServiceIntegrationTests : AbstractTest
{
    private SmartGroupService _service = null!;
    private SmartGroupStorageManager _storageManager = null!;
    private CachedUserService _userService = null!;
    private IExternalUserService _externalUserService = null!;
    private IUserCacheManager _cacheManager = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        // Initialize storage manager
        _storageManager = new SmartGroupStorageManager(
            _config.ConnectionStrings.Storage,
            GetLogger<SmartGroupStorageManager>()
        );

        // Initialize Graph client and cache manager
        var clientSecretCredential = new ClientSecretCredential(
            _config.GraphConfig.TenantId,
            _config.GraphConfig.ClientId,
            _config.GraphConfig.ClientSecret);

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

        // Use in-memory cache for testing
        var cacheConfig = new UserCacheConfig();
        var copilotStatsLoader = new GraphCopilotStatsLoader(
            GetLogger<GraphCopilotStatsLoader>(),
            cacheConfig,
            _config.GraphConfig);
        var dataLoader = new GraphUserDataLoader(graphClient, GetLogger<GraphUserDataLoader>(), copilotStatsLoader, cacheConfig);
        var storage = new InMemoryCacheStorage();
        _cacheManager = new UserCacheManager(dataLoader, storage, cacheConfig, GetLogger<UserCacheManager>());

        // Initialize external user service (direct API access)
        _externalUserService = new GraphUserService(
            _config.GraphConfig,
            GetLogger<GraphUserService>()
        );

        // Initialize CachedUserService (cache-first logic)
        _userService = new CachedUserService(
            _cacheManager,
            _externalUserService,
            GetLogger<CachedUserService>()
        );

        // Initialize service without AI for basic tests
        _service = new SmartGroupService(
            _storageManager,
            _userService,
            GetLogger<SmartGroupService>(),
            null // No AI service for basic tests
        );

        // Ensure initialization by making a dummy call
        await Task.CompletedTask;
    }

    [TestMethod]
    public async Task CreateAndGetSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in the Sales department";
        var createdBy = "test@example.com";

        // Act
        var createdGroup = await _service.CreateSmartGroup(groupName, description, createdBy);
        var retrievedGroup = await _service.GetSmartGroup(createdGroup.Id);

        // Assert
        Assert.IsNotNull(retrievedGroup);
        Assert.AreEqual(groupName, retrievedGroup.Name);
        Assert.AreEqual(description, retrievedGroup.Description);
        Assert.AreEqual(createdBy, retrievedGroup.CreatedByUpn);

        // Cleanup
        await _service.DeleteSmartGroup(createdGroup.Id);
    }

    [TestMethod]
    public async Task UpdateSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in the Sales department";
        var createdBy = "test@example.com";

        var createdGroup = await _service.CreateSmartGroup(groupName, description, createdBy);

        var newName = $"Updated Group {Guid.NewGuid()}";
        var newDescription = "Users in the Marketing department";

        // Act
        var updatedGroup = await _service.UpdateSmartGroup(
            createdGroup.Id,
            newName,
            newDescription
        );

        // Assert
        Assert.IsNotNull(updatedGroup);
        Assert.AreEqual(newName, updatedGroup.Name);
        Assert.AreEqual(newDescription, updatedGroup.Description);

        // Cleanup
        await _service.DeleteSmartGroup(createdGroup.Id);
    }

    [TestMethod]
    public async Task GetAllSmartGroups_Success()
    {
        // Arrange
        var groupName1 = $"Test Group 1 {Guid.NewGuid()}";
        var groupName2 = $"Test Group 2 {Guid.NewGuid()}";
        var description = "Test description";
        var createdBy = "test@example.com";

        var group1 = await _service.CreateSmartGroup(groupName1, description, createdBy);
        var group2 = await _service.CreateSmartGroup(groupName2, description, createdBy);

        // Act
        var allGroups = await _service.GetAllSmartGroups();

        // Assert
        Assert.IsTrue(allGroups.Count >= 2);
        Assert.IsTrue(allGroups.Any(g => g.Id == group1.Id));
        Assert.IsTrue(allGroups.Any(g => g.Id == group2.Id));

        // Cleanup
        await _service.DeleteSmartGroup(group1.Id);
        await _service.DeleteSmartGroup(group2.Id);
    }

    [TestMethod]
    public async Task DeleteSmartGroup_RemovesGroup()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Test description";
        var createdBy = "test@example.com";

        var createdGroup = await _service.CreateSmartGroup(groupName, description, createdBy);

        // Act
        await _service.DeleteSmartGroup(createdGroup.Id);
        var retrievedGroup = await _service.GetSmartGroup(createdGroup.Id);

        // Assert
        Assert.IsNull(retrievedGroup);
    }

    [TestMethod]
    public void IsAIEnabled_WhenNoAIService_ReturnsFalse()
    {
        // Assert
        Assert.IsFalse(_service.IsAIEnabled);
    }

    [TestMethod]
    public async Task ResolveSmartGroupMembers_WithoutAI_ThrowsException()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in the Sales department";
        var createdBy = "test@example.com";

        var createdGroup = await _service.CreateSmartGroup(groupName, description, createdBy);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _service.ResolveSmartGroupMembers(createdGroup.Id)
        );

        // Cleanup
        await _service.DeleteSmartGroup(createdGroup.Id);
    }

    [TestMethod]
    public async Task PreviewSmartGroupMembers_WithoutAI_ThrowsException()
    {
        // Arrange
        var description = "Users in the Sales department";

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _service.PreviewSmartGroupMembers(description)
        );
    }

    [TestMethod]
    public async Task GetSmartGroupUpns_WithoutAI_ThrowsException()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in the Sales department";
        var createdBy = "test@example.com";

        var createdGroup = await _service.CreateSmartGroup(groupName, description, createdBy);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await _service.GetSmartGroupUpns(createdGroup.Id)
        );

        // Cleanup
        await _service.DeleteSmartGroup(createdGroup.Id);
    }
}
