using Common.Engine.Models;
using Common.Engine.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for the AI Foundry user-chunking helper. Verifies the smart-group
/// resolution batching logic that prevents oversized prompts at tenant scale.
/// </summary>
[TestClass]
public class AIFoundryChunkUsersTests
{
    private static List<EnrichedUserInfo> CreateUsers(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new EnrichedUserInfo { Id = $"u{i}", UserPrincipalName = $"u{i}@x.com" })
            .ToList();

    [TestMethod]
    public void ChunkUsers_EmptyInput_ReturnsNoChunks()
    {
        var chunks = AIFoundryService.ChunkUsers(new List<EnrichedUserInfo>(), 100);
        Assert.AreEqual(0, chunks.Count);
    }

    [TestMethod]
    public void ChunkUsers_FewerThanChunkSize_ReturnsSingleChunk()
    {
        var users = CreateUsers(7);
        var chunks = AIFoundryService.ChunkUsers(users, 100);
        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual(7, chunks[0].Count);
    }

    [TestMethod]
    public void ChunkUsers_ExactlyChunkSize_ReturnsSingleChunk()
    {
        var users = CreateUsers(100);
        var chunks = AIFoundryService.ChunkUsers(users, 100);
        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual(100, chunks[0].Count);
    }

    [TestMethod]
    public void ChunkUsers_OverChunkSize_SplitsAtBoundary()
    {
        var users = CreateUsers(101);
        var chunks = AIFoundryService.ChunkUsers(users, 100);
        Assert.AreEqual(2, chunks.Count);
        Assert.AreEqual(100, chunks[0].Count);
        Assert.AreEqual(1, chunks[1].Count);
    }

    [TestMethod]
    public void ChunkUsers_LargeTenant_SplitsAndPreservesOrder()
    {
        var users = CreateUsers(2_550);
        var chunks = AIFoundryService.ChunkUsers(users, 100);
        Assert.AreEqual(26, chunks.Count);
        Assert.IsTrue(chunks.Take(25).All(c => c.Count == 100));
        Assert.AreEqual(50, chunks[25].Count);

        // First and last UPN preserved end-to-end.
        Assert.AreEqual("u0@x.com", chunks[0][0].UserPrincipalName);
        Assert.AreEqual("u2549@x.com", chunks[25][^1].UserPrincipalName);
    }

    [TestMethod]
    public void ChunkUsers_NonStandardChunkSize_HonoursIt()
    {
        var users = CreateUsers(10);
        var chunks = AIFoundryService.ChunkUsers(users, 3);
        CollectionAssert.AreEqual(new[] { 3, 3, 3, 1 }, chunks.Select(c => c.Count).ToArray());
    }

    [TestMethod]
    public void ChunkUsers_InvalidChunkSize_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AIFoundryService.ChunkUsers(CreateUsers(5), 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => AIFoundryService.ChunkUsers(CreateUsers(5), -1));
    }
}
