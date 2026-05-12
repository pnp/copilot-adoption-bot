using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

/// <summary>
/// State of an asynchronous smart group resolution job.
/// </summary>
public enum SmartGroupResolutionJobState
{
    Queued,
    Running,
    Succeeded,
    Failed
}

/// <summary>
/// Snapshot of a single resolution job.
/// </summary>
public class SmartGroupResolutionJob
{
    public required string JobId { get; init; }
    public required string SmartGroupId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SmartGroupResolutionJobState State { get; set; } = SmartGroupResolutionJobState.Queued;
    public string? CurrentStep { get; set; }
    public string? Error { get; set; }
    public int? MemberCount { get; set; }
    public bool FromCache { get; set; }
}

/// <summary>
/// In-memory tracker for asynchronous smart group resolution jobs.
/// Singleton — jobs do not survive a process restart, which is fine because
/// callers can simply re-queue.
/// </summary>
public class SmartGroupResolutionJobTracker
{
    /// <summary>
    /// Jobs older than this are eligible for cleanup once they reach a terminal state.
    /// </summary>
    public static readonly TimeSpan CompletedJobRetention = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, SmartGroupResolutionJob> _jobs = new();
    private readonly ILogger<SmartGroupResolutionJobTracker> _logger;
    private readonly Func<DateTime> _clock;

    public SmartGroupResolutionJobTracker(ILogger<SmartGroupResolutionJobTracker> logger)
        : this(logger, () => DateTime.UtcNow) { }

    internal SmartGroupResolutionJobTracker(ILogger<SmartGroupResolutionJobTracker> logger, Func<DateTime> clock)
    {
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Create and register a new job in <see cref="SmartGroupResolutionJobState.Queued"/> state.
    /// </summary>
    public SmartGroupResolutionJob CreateJob(string smartGroupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(smartGroupId);

        CleanupExpiredJobs();

        var job = new SmartGroupResolutionJob
        {
            JobId = Guid.NewGuid().ToString("n"),
            SmartGroupId = smartGroupId,
            CreatedAt = _clock(),
            CurrentStep = "Queued"
        };

        if (!_jobs.TryAdd(job.JobId, job))
        {
            throw new InvalidOperationException("Failed to register resolution job (id collision)");
        }

        _logger.LogInformation("Queued smart group resolution job {JobId} for group {SmartGroupId}", job.JobId, smartGroupId);
        return job;
    }

    /// <summary>
    /// Look up a job by id. Returns null if unknown or already evicted.
    /// </summary>
    public SmartGroupResolutionJob? GetJob(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <summary>
    /// Mark the job as actively running and update its current step.
    /// </summary>
    public void MarkRunning(string jobId, string currentStep)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = SmartGroupResolutionJobState.Running;
            job.CurrentStep = currentStep;
            job.StartedAt ??= _clock();
        }
    }

    /// <summary>
    /// Update the human-readable step on a running job (e.g. "Calling AI Foundry...").
    /// </summary>
    public void UpdateStep(string jobId, string currentStep)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.CurrentStep = currentStep;
        }
    }

    /// <summary>
    /// Mark the job successful with the resolved member count.
    /// </summary>
    public void MarkSucceeded(string jobId, int memberCount, bool fromCache)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = SmartGroupResolutionJobState.Succeeded;
            job.MemberCount = memberCount;
            job.FromCache = fromCache;
            job.CurrentStep = fromCache ? "Returned cached members" : "Completed";
            job.CompletedAt = _clock();
        }
    }

    /// <summary>
    /// Mark the job failed and record the error message.
    /// </summary>
    public void MarkFailed(string jobId, string error)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = SmartGroupResolutionJobState.Failed;
            job.Error = error;
            job.CurrentStep = "Failed";
            job.CompletedAt = _clock();
        }
    }

    /// <summary>
    /// Remove jobs that completed more than <see cref="CompletedJobRetention"/> ago.
    /// </summary>
    public void CleanupExpiredJobs()
    {
        var cutoff = _clock() - CompletedJobRetention;
        foreach (var kvp in _jobs)
        {
            var job = kvp.Value;
            if (job.CompletedAt.HasValue && job.CompletedAt.Value < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
