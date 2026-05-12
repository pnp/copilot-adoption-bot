using Engine.Models;
using Engine.Services.UserCache;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Fakes;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="CopilotStatsService.GetCopilotUsageStatsAsync"/>.
/// The other half of <see cref="CopilotStatsService"/> (the table-storage update) is exercised
/// by <c>CopilotStatsServiceTests</c> in the integration suite because it takes a raw
/// <c>TableClient</c>.
/// </summary>
[TestClass]
public class CopilotStatsServiceLoaderTests
{
    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_DelegatesToLoader_AndReturnsItsResult()
    {
        var records = new List<CopilotUsageRecord>
        {
            new() { UserPrincipalName = "a@contoso.com", LastActivityDate = DateTime.UtcNow.AddDays(-1) }
        };
        var loader = new FakeCopilotStatsLoader(records);
        var service = new CopilotStatsService(NullLogger<CopilotStatsService>.Instance, loader);

        var result = await service.GetCopilotUsageStatsAsync();

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.Records.Count);
        Assert.AreEqual("a@contoso.com", result.Records[0].UserPrincipalName);
    }

    [TestMethod]
    public async Task GetCopilotUsageStatsAsync_LoaderFailure_PropagatesFailureResult()
    {
        var loader = new FailingCopilotStatsLoader();
        var service = new CopilotStatsService(NullLogger<CopilotStatsService>.Instance, loader);

        var result = await service.GetCopilotUsageStatsAsync();

        Assert.IsFalse(result.Success);
        Assert.AreEqual(500, result.StatusCode);
        Assert.AreEqual("boom", result.ErrorMessage);
        Assert.AreEqual(0, result.Records.Count);
    }

    private sealed class FailingCopilotStatsLoader : ICopilotStatsLoader
    {
        public Task<CopilotStatsResult> GetCopilotUsageStatsAsync() => Task.FromResult(new CopilotStatsResult
        {
            Success = false,
            StatusCode = 500,
            ErrorMessage = "boom"
        });
    }
}
