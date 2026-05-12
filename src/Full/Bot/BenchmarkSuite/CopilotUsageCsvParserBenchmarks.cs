using System.Text;
using BenchmarkDotNet.Attributes;
using Common.Engine.Services.UserCache;

namespace Engine.Benchmarks;
public class CopilotUsageCsvParserBenchmarks
{
    private string _csv = string.Empty;
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder(capacity: 1024 * 1024);
        sb.Append("User Principal Name,").Append("Last Activity Date,").Append("Copilot Chat Last Activity Date,").Append("Microsoft Teams Copilot Last Activity Date,").Append("Word Copilot Last Activity Date,").Append("Excel Copilot Last Activity Date,").Append("PowerPoint Copilot Last Activity Date,").Append("Outlook Copilot Last Activity Date,").Append("OneNote Copilot Last Activity Date,").Append("Loop Copilot Last Activity Date\r\n");
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < RowCount; i++)
        {
            var d = baseDate.AddDays(i % 365).ToString("yyyy-MM-dd");
            sb.Append("user").Append(i).Append("@contoso.com,").Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append(',').Append(d).Append("\r\n");
        }

        _csv = sb.ToString();
    }

    [Benchmark]
    public int Parse() => CopilotUsageCsvParser.Parse(_csv).Count;
}