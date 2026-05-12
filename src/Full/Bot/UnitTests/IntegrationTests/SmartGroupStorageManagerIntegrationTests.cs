using Engine;
using Engine.Storage;

namespace UnitTests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="SmartGroupStorageManager"/>.
/// </summary>
[TestClass]
public class SmartGroupStorageManagerIntegrationTests : AbstractTest
{
    private SmartGroupStorageManager _smartGroupStorage = null!;

    [TestInitialize]
    public void Initialize()
    {
        _smartGroupStorage = new SmartGroupStorageManager(
            GetStorageAuthConfig(),
            GetLogger<SmartGroupStorageManager>()
        );
    }

    [TestMethod]
    public async Task SmartGroupStorage_CreateAndGetSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var description = "Users in Sales department";
        var createdBy = "test@example.com";

        // Act
        var created = await _smartGroupStorage.CreateSmartGroup(groupName, description, createdBy);
        var retrieved = await _smartGroupStorage.GetSmartGroup(created.RowKey);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(groupName, retrieved.Name);
        Assert.AreEqual(description, retrieved.Description);
        Assert.AreEqual(createdBy, retrieved.CreatedByUpn);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(created.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_UpdateSmartGroup_Success()
    {
        // Arrange
        var groupName = $"Test Group {Guid.NewGuid()}";
        var created = await _smartGroupStorage.CreateSmartGroup(groupName, "Original desc", "test@example.com");

        var newName = $"Updated Group {Guid.NewGuid()}";
        var newDescription = "Updated description";

        // Act
        var updated = await _smartGroupStorage.UpdateSmartGroup(created.RowKey, newName, newDescription);
        var retrieved = await _smartGroupStorage.GetSmartGroup(created.RowKey);

        // Assert
        Assert.AreEqual(newName, retrieved!.Name);
        Assert.AreEqual(newDescription, retrieved.Description);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(created.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_CacheAndGetSmartGroupMembers_Success()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");

        var members = new List<SmartGroupMemberCacheEntity>
        {
            new SmartGroupMemberCacheEntity
            {
                RowKey = "user1@example.com",
                DisplayName = "User One",
                Department = "Sales",
                JobTitle = "Sales Manager",
                ConfidenceScore = 0.95
            },
            new SmartGroupMemberCacheEntity
            {
                RowKey = "user2@example.com",
                DisplayName = "User Two",
                Department = "Sales",
                JobTitle = "Sales Rep",
                ConfidenceScore = 0.87
            }
        };

        // Act
        await _smartGroupStorage.CacheSmartGroupMembers(group.RowKey, members);
        var cached = await _smartGroupStorage.GetCachedSmartGroupMembers(group.RowKey);

        // Assert
        Assert.AreEqual(2, cached.Count);
        Assert.IsTrue(cached.Any(m => m.RowKey == "user1@example.com"));
        Assert.IsTrue(cached.Any(m => m.RowKey == "user2@example.com"));

        var user1 = cached.First(m => m.RowKey == "user1@example.com");
        Assert.AreEqual("User One", user1.DisplayName);
        Assert.AreEqual(0.95, user1.ConfidenceScore);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_UpdateSmartGroupResolution_Success()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");
        var memberCount = 5;

        // Act
        await _smartGroupStorage.UpdateSmartGroupResolution(group.RowKey, memberCount);
        var retrieved = await _smartGroupStorage.GetSmartGroup(group.RowKey);

        // Assert
        Assert.IsNotNull(retrieved!.LastResolvedDate);
        Assert.AreEqual(memberCount, retrieved.LastResolvedMemberCount);
        Assert.IsTrue((DateTime.UtcNow - retrieved.LastResolvedDate.Value).TotalMinutes < 1);

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_GetAllSmartGroups_Success()
    {
        // Arrange
        var group1 = await _smartGroupStorage.CreateSmartGroup($"Group 1 {Guid.NewGuid()}", "Test 1", "test@example.com");
        var group2 = await _smartGroupStorage.CreateSmartGroup($"Group 2 {Guid.NewGuid()}", "Test 2", "test@example.com");

        // Act
        var all = await _smartGroupStorage.GetAllSmartGroups();

        // Assert
        Assert.IsTrue(all.Count >= 2);
        Assert.IsTrue(all.Any(g => g.RowKey == group1.RowKey));
        Assert.IsTrue(all.Any(g => g.RowKey == group2.RowKey));

        // Cleanup
        await _smartGroupStorage.DeleteSmartGroup(group1.RowKey);
        await _smartGroupStorage.DeleteSmartGroup(group2.RowKey);
    }

    [TestMethod]
    public async Task SmartGroupStorage_DeleteSmartGroup_RemovesGroup()
    {
        // Arrange
        var group = await _smartGroupStorage.CreateSmartGroup($"Group {Guid.NewGuid()}", "Test", "test@example.com");

        // Act
        await _smartGroupStorage.DeleteSmartGroup(group.RowKey);
        var retrieved = await _smartGroupStorage.GetSmartGroup(group.RowKey);

        // Assert
        Assert.IsNull(retrieved);
    }
}
