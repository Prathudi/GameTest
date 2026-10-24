using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameTest.Runner;

/// <summary>Facts about the whole run that are not part of any single test.</summary>
public sealed record RunInfo(DateTimeOffset StartedAt, TimeSpan Duration, string GameExe, string TestFolder);

public sealed record ReportSummary(int Total, int Passed, int Failed, int TimedOut)
{
    public static ReportSummary From(IReadOnlyList<TestResult> results) => new(
        results.Count,
        results.Count(r => r.Outcome == TestOutcome.Pass),
        results.Count(r => r.Outcome == TestOutcome.Fail),
        results.Count(r => r.Outcome == TestOutcome.Timeout));
}

/// <summary>One test in the report. File paths are relative to the folder that holds report.json.</summary>
public sealed record TestReportEntry(
    string Name,
    string Outcome,
    long DurationMs,
    string? FailureReason,
    int? FailedStep,
    string? Screenshot,
    string? ScreenshotError,
    string? LogFile,
    IReadOnlyList<string> Log);

public sealed record RunReport(
    string StartedAt,
    long DurationMs,
    string Game,
    string TestFolder,
    ReportSummary Summary,
    IReadOnlyList<TestReportEntry> Tests);

/// <summary>Creates the output folder for a run and writes report.json into it.</summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        // The file is read by people and tools, not embedded in a web page, so keep it readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Creates results\run_YYYYMMDD_HHMMSS (adds _2, _3 ... if it already exists).</summary>
    public static string CreateRunDirectory(string root, DateTimeOffset startedAt)
    {
        string baseName = "run_" + startedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string path = Path.Combine(root, baseName);
        for (int n = 2; Directory.Exists(path); n++)
        {
            path = Path.Combine(root, $"{baseName}_{n}");
        }
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Writes report.json into the folder and returns its full path.</summary>
    public static string Write(string runDirectory, RunInfo info, IReadOnlyList<TestResult> results)
    {
        var report = new RunReport(
            info.StartedAt.ToString("o", CultureInfo.InvariantCulture),
            (long)info.Duration.TotalMilliseconds,
            Path.GetFullPath(info.GameExe),
            Path.GetFullPath(info.TestFolder),
            ReportSummary.From(results),
            results.Select(r => new TestReportEntry(
                r.Name,
                r.Outcome.ToLabel(),
                (long)r.Duration.TotalMilliseconds,
                r.FailureReason,
                r.FailedStep,
                r.ScreenshotFile,
                r.ScreenshotError,
                r.LogFile,
                r.Log)).ToList());

        string path = Path.GetFullPath(Path.Combine(runDirectory, "report.json"));
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options), new UTF8Encoding(false));
        return path;
    }
}
