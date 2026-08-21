using System.Diagnostics.Metrics;

namespace Engine.Services;

/// <summary>
/// Custom metrics for the nudge pipeline.
///
/// <para>
/// Before this, the only observability was <c>ILogger</c> text - one Information entry per
/// message, i.e. 150,000 log lines per batch. That is expensive to ingest and makes the signal
/// harder to find, not easier, and several of the failure modes found during the pre-release
/// review are silent by construction: a message can be recorded as delivered without ever being
/// sent. Counters make those visible and alertable.
/// </para>
///
/// <para>
/// Uses <c>System.Diagnostics.Metrics</c>, which the Application Insights SDK collects natively.
/// </para>
/// </summary>
public static class NudgeMetrics
{
    public const string MeterName = "AdoptionBot.Nudge";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Deliveries successfully sent.</summary>
    public static readonly Counter<long> Sent =
        Meter.CreateCounter<long>("nudge.sent", "messages", "Nudges delivered to a user");

    /// <summary>Deliveries that failed permanently.</summary>
    public static readonly Counter<long> Failed =
        Meter.CreateCounter<long>("nudge.failed", "messages", "Nudges that failed permanently");

    /// <summary>Deliveries deferred for retry (throttling, transport errors).</summary>
    public static readonly Counter<long> Transient =
        Meter.CreateCounter<long>("nudge.transient", "messages", "Sends deferred for retry after a transient failure");

    /// <summary>Deliveries awaiting the user opening Teams after an app install.</summary>
    public static readonly Counter<long> AwaitingInstall =
        Meter.CreateCounter<long>("nudge.awaiting_install", "messages", "Deliveries waiting on the user opening Teams");

    /// <summary>Deliveries dropped because their batch was cancelled or deleted.</summary>
    public static readonly Counter<long> Dropped =
        Meter.CreateCounter<long>("nudge.dropped", "messages", "Deliveries dropped for a cancelled or deleted batch");

    /// <summary>End-to-end send latency.</summary>
    public static readonly Histogram<double> SendDuration =
        Meter.CreateHistogram<double>("nudge.send_duration", "ms", "Time to deliver a single nudge");

    /// <summary>Recipients expanded into delivery rows.</summary>
    public static readonly Counter<long> Expanded =
        Meter.CreateCounter<long>("nudge.expanded", "recipients", "Recipients expanded into delivery rows");

    /// <summary>
    /// Observable queue depth. Registered once at startup with a callback so a stalled or
    /// backing-up drain is visible continuously, rather than only when someone loads the
    /// diagnostics page.
    /// </summary>
    public static void RegisterQueueDepth(Func<int> readDepth)
    {
        ArgumentNullException.ThrowIfNull(readDepth);
        Meter.CreateObservableGauge("nudge.queue_depth", readDepth, "messages", "Pending deliveries in the queue");
    }

    /// <summary>
    /// Record the outcome of a send with the batch id as a dimension.
    /// </summary>
    public static void RecordOutcome(SendDisposition disposition, string batchId, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("batch", batchId);

        switch (disposition)
        {
            case SendDisposition.Delivered:
                Sent.Add(1, tag);
                SendDuration.Record(durationMs, tag);
                break;
            case SendDisposition.PermanentFailure:
                Failed.Add(1, tag);
                break;
            case SendDisposition.TransientFailure:
                Transient.Add(1, tag);
                break;
            case SendDisposition.AwaitingInstall:
                AwaitingInstall.Add(1, tag);
                break;
        }
    }
}
