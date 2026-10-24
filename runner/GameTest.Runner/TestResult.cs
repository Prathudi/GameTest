namespace GameTest.Runner;

public enum TestOutcome { Pass, Fail, Timeout }

public static class TestOutcomeExtensions
{
    /// <summary>"PASS", "FAIL" or "TIMEOUT": the spelling used in the console and in report.json.</summary>
    public static string ToLabel(this TestOutcome outcome) => outcome switch
    {
        TestOutcome.Pass => "PASS",
        TestOutcome.Fail => "FAIL",
        TestOutcome.Timeout => "TIMEOUT",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}

/// <summary>What happened when one test ran.</summary>
public sealed class TestResult
{
    public required string Name { get; init; }
    public required TestOutcome Outcome { get; init; }
    public required TimeSpan Duration { get; init; }

    /// <summary>Why a test did not pass. Null when it passed.</summary>
    public string? FailureReason { get; init; }

    /// <summary>The 1-based number of the step that failed, if any.</summary>
    public int? FailedStep { get; init; }

    /// <summary>One line per step performed, with the time it happened.</summary>
    public required IReadOnlyList<string> Log { get; init; }

    // Evidence files. Paths are relative to the run folder and use '/' as the separator.

    /// <summary>The screenshot taken when the test did not pass, if one could be taken.</summary>
    public string? ScreenshotFile { get; set; }

    /// <summary>Why there is no screenshot, when the test did not pass and none was taken.</summary>
    public string? ScreenshotError { get; set; }

    /// <summary>The text log of this test.</summary>
    public string? LogFile { get; set; }

    /// <summary>Pictures taken on purpose by "screenshot" steps (paths relative to the run folder).</summary>
    public List<string> Checkpoints { get; init; } = new();
}

