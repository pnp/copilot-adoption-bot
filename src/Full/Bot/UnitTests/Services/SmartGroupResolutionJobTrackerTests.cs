using Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Services;

/// <summary>
/// Pure unit tests for <see cref="SmartGroupResolutionJobTracker"/> — exercises state
/// transitions and the completed-job retention/cleanup behaviour without requiring
/// any Azure or Graph dependencies.
/// </summary>
[TestClass]
public class SmartGroupResolutionJobTrackerTests
{
    private static SmartGroupResolutionJobTracker MakeTracker(Func<DateTime>? clock = null) =>
        new(NullLogger<SmartGroupResolutionJobTracker>.Instance, clock ?? (() => DateTime.UtcNow));

    [TestMethod]
    public void CreateJob_NewJob_IsQueuedAndRetrievable()
    {
        var tracker = MakeTracker();

        var job = tracker.CreateJob("sg-1");

        Assert.IsFalse(string.IsNullOrWhiteSpace(job.JobId));
        Assert.AreEqual("sg-1", job.SmartGroupId);
        Assert.AreEqual(SmartGroupResolutionJobState.Queued, job.State);
        Assert.AreEqual("Queued", job.CurrentStep);

        var fetched = tracker.GetJob(job.JobId);
        Assert.IsNotNull(fetched);
        Assert.AreSame(job, fetched);
    }

    [TestMethod]
    public void CreateJob_BlankSmartGroupId_Throws()
    {
        var tracker = MakeTracker();

        Assert.ThrowsException<ArgumentException>(() => tracker.CreateJob(""));
        Assert.ThrowsException<ArgumentException>(() => tracker.CreateJob("   "));
    }

    [TestMethod]
    public void GetJob_UnknownId_ReturnsNull()
    {
        var tracker = MakeTracker();
        Assert.IsNull(tracker.GetJob("does-not-exist"));
    }

    [TestMethod]
    public void MarkRunning_TransitionsState_AndSetsStartedAt()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");

        time = time.AddSeconds(5);
        tracker.MarkRunning(job.JobId, "Loading users");

        Assert.AreEqual(SmartGroupResolutionJobState.Running, job.State);
        Assert.AreEqual("Loading users", job.CurrentStep);
        Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 5, DateTimeKind.Utc), job.StartedAt);
    }

    [TestMethod]
    public void MarkRunning_TwiceDoesNotResetStartedAt()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");

        time = time.AddSeconds(5);
        tracker.MarkRunning(job.JobId, "Loading users");
        var firstStart = job.StartedAt;

        time = time.AddSeconds(10);
        tracker.MarkRunning(job.JobId, "Calling AI");

        Assert.AreEqual(firstStart, job.StartedAt);
        Assert.AreEqual("Calling AI", job.CurrentStep);
    }

    [TestMethod]
    public void UpdateStep_OnlyChangesCurrentStep()
    {
        var tracker = MakeTracker();
        var job = tracker.CreateJob("sg-1");
        tracker.MarkRunning(job.JobId, "step-a");

        tracker.UpdateStep(job.JobId, "step-b");

        Assert.AreEqual("step-b", job.CurrentStep);
        Assert.AreEqual(SmartGroupResolutionJobState.Running, job.State);
    }

    [TestMethod]
    public void MarkSucceeded_RecordsResultAndCompletion()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");
        tracker.MarkRunning(job.JobId, "step");

        time = time.AddSeconds(20);
        tracker.MarkSucceeded(job.JobId, memberCount: 42, fromCache: false);

        Assert.AreEqual(SmartGroupResolutionJobState.Succeeded, job.State);
        Assert.AreEqual(42, job.MemberCount);
        Assert.IsFalse(job.FromCache);
        Assert.AreEqual("Completed", job.CurrentStep);
        Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 20, DateTimeKind.Utc), job.CompletedAt);
    }

    [TestMethod]
    public void MarkSucceeded_FromCache_SetsCacheStep()
    {
        var tracker = MakeTracker();
        var job = tracker.CreateJob("sg-1");

        tracker.MarkSucceeded(job.JobId, memberCount: 3, fromCache: true);

        Assert.IsTrue(job.FromCache);
        Assert.AreEqual("Returned cached members", job.CurrentStep);
    }

    [TestMethod]
    public void MarkFailed_RecordsErrorAndTerminalState()
    {
        var tracker = MakeTracker();
        var job = tracker.CreateJob("sg-1");
        tracker.MarkRunning(job.JobId, "step");

        tracker.MarkFailed(job.JobId, "AI Foundry is not configured");

        Assert.AreEqual(SmartGroupResolutionJobState.Failed, job.State);
        Assert.AreEqual("AI Foundry is not configured", job.Error);
        Assert.AreEqual("Failed", job.CurrentStep);
        Assert.IsNotNull(job.CompletedAt);
    }

    [TestMethod]
    public void CleanupExpiredJobs_DropsCompletedJobsPastRetention()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");
        tracker.MarkSucceeded(job.JobId, memberCount: 1, fromCache: false);

        time = time + SmartGroupResolutionJobTracker.CompletedJobRetention + TimeSpan.FromSeconds(1);

        tracker.CleanupExpiredJobs();

        Assert.IsNull(tracker.GetJob(job.JobId));
    }

    [TestMethod]
    public void CleanupExpiredJobs_KeepsRunningJobsRegardlessOfAge()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");
        tracker.MarkRunning(job.JobId, "still going");

        // Jump way past the retention window — running jobs must not be evicted.
        time = time + SmartGroupResolutionJobTracker.CompletedJobRetention + TimeSpan.FromMinutes(60);

        tracker.CleanupExpiredJobs();

        Assert.IsNotNull(tracker.GetJob(job.JobId));
    }

    [TestMethod]
    public void CleanupExpiredJobs_KeepsRecentlyCompletedJobs()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var tracker = MakeTracker(() => time);
        var job = tracker.CreateJob("sg-1");
        tracker.MarkSucceeded(job.JobId, memberCount: 1, fromCache: false);

        time = time + TimeSpan.FromSeconds(1);

        tracker.CleanupExpiredJobs();

        Assert.IsNotNull(tracker.GetJob(job.JobId));
    }
}
