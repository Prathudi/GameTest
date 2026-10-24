using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace GameTest.Runner;

/// <summary>Runs many tests, each on its own freshly started game.</summary>
public static class TestRunner
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the tests one after another. A failing test never stops the others.
    /// If <paramref name="outputDirectory"/> is given, each test gets a subfolder there with
    /// its log and, when it did not pass, a screenshot. <paramref name="onResult"/> is called
    /// as soon as each test finishes.
    /// </summary>
    public static async Task<List<TestResult>> RunAllAsync(
        IReadOnlyList<TestDefinition> tests,
        string gameExe,
        string? outputDirectory = null,
        Action<TestResult>? onResult = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        foreach (TestDefinition test in tests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestResult result = await RunOneAsync(test, gameExe, outputDirectory, cancellationToken);
            results.Add(result);
            onResult?.Invoke(result);
        }
        return results;
    }

    /// <summary>Starts a game, runs one test on it, collects evidence, and always shuts the game down.</summary>
    public static async Task<TestResult> RunOneAsync(
        TestDefinition test, string gameExe, string? outputDirectory = null, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        string? testDirectory = outputDirectory is null ? null : Path.Combine(outputDirectory, test.Name);
        GameProcess? game = null;
        TestResult result;
        try
        {
            game = await GameProcess.StartAsync(gameExe, StartupTimeout, cancellationToken);
            using AgentClient client = await game.ConnectAsync(StartupTimeout, cancellationToken);

            CheckpointScreenshot? checkpoint = null;
            if (testDirectory is not null && OperatingSystem.IsWindows())
            {
                checkpoint = MakeCheckpointTaker(game, test.Name, testDirectory);
            }
            result = await TestExecutor.RunAsync(test, client, cancellationToken, checkpoint);
            if (result.Outcome != TestOutcome.Pass)
            {
                result = await ExplainIfGameDiedAsync(result, game);
                // The picture must be taken now, while the game window still exists.
                if (testDirectory is not null) await CaptureFailureAsync(result, game, testDirectory);
            }

            await game.StopAsync(client);
        }
        catch (AgentTimeoutException ex)
        {
            result = Failure(test, TestOutcome.Timeout, $"the game could not be started: {ex.Message}", clock);
        }
        catch (GameCrashedException ex)
        {
            result = Failure(test, TestOutcome.Fail, $"the game crashed while starting: {ex.Message}", clock);
        }
        catch (FileNotFoundException ex)
        {
            result = Failure(test, TestOutcome.Fail, ex.Message, clock);
        }
        catch (AgentException ex)
        {
            result = Failure(test, TestOutcome.Fail, $"the game could not be started: {ex.Message}", clock);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bug or an odd environment problem in ONE test must not end the whole run.
            result = Failure(test, TestOutcome.Fail, $"unexpected error: {ex.GetType().Name}: {ex.Message}", clock);
        }
        finally
        {
            if (game is not null) await game.DisposeAsync();
        }

        if (testDirectory is not null) WriteLog(test, result, testDirectory);
        return result;
    }

    // After a failed test: if the game process has exited, say so, with its exit code and output.
    private static async Task<TestResult> ExplainIfGameDiedAsync(TestResult result, GameProcess game)
    {
        // The process may need a moment to be reported as exited after the connection drops.
        for (int i = 0; i < 10 && !game.HasExited; i++) await Task.Delay(50);
        if (!game.HasExited) return result;

        var log = new List<string>(result.Log) { $"the game process had exited (exit code {game.ExitCode})" };
        log.AddRange(game.DescribeOutput().Split('\n'));
        return new TestResult
        {
            Name = result.Name,
            Outcome = TestOutcome.Fail,  // a game that dies during a test is a failure
            Duration = result.Duration,
            FailureReason = $"{result.FailureReason} (the game process had exited, exit code {game.ExitCode})",
            FailedStep = result.FailedStep,
            Checkpoints = result.Checkpoints,
            Log = log,
        };
    }

    // Saves a picture of the game window. This is evidence, so it is best-effort: whatever goes
    // wrong is recorded in the result and must never replace the test's real outcome.
    private static async Task CaptureFailureAsync(TestResult result, GameProcess game, string testDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            result.ScreenshotError = "screenshots are only supported on Windows";
            return;
        }
        if (game.HasExited)
        {
            result.ScreenshotError = "the game had already exited, so there was no window to capture";
            return;
        }

        try
        {
            IntPtr window = await WindowCapture.WaitForWindowAsync(game, TimeSpan.FromSeconds(2));
            string fileName = $"{result.Name}_{result.Outcome.ToLabel()}_{DateTime.Now:HHmmss}.png";
            await WindowCapture.CaptureClientAreaAsync(window, Path.Combine(testDirectory, fileName));
            result.ScreenshotFile = $"{result.Name}/{fileName}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.ScreenshotError = ex is ScreenshotException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    // Writes the test's log file: a short header followed by the step log.
    private static void WriteLog(TestDefinition test, TestResult result, string testDirectory)
    {
        try
        {
            Directory.CreateDirectory(testDirectory);
            string fileName = $"{test.Name}.log";

            var lines = new List<string>
            {
                $"test:       {test.Name}",
                $"file:       {test.SourceFile}",
                $"outcome:    {result.Outcome.ToLabel()}",
                $"duration:   {result.Duration.TotalMilliseconds:0} ms",
            };
            if (result.FailureReason is not null) lines.Add($"reason:     {result.FailureReason}");
            if (result.ScreenshotFile is not null) lines.Add($"screenshot: {result.ScreenshotFile}");
            if (result.ScreenshotError is not null) lines.Add($"no screenshot: {result.ScreenshotError}");
            lines.Add("");
            lines.AddRange(result.Log);

            File.WriteAllLines(Path.Combine(testDirectory, fileName), lines, new UTF8Encoding(false));
            result.LogFile = $"{test.Name}/{fileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Evidence is best-effort: a full disk must not turn a result into a crash.
        }
    }

    // Builds the function that "screenshot" steps use. Like failure pictures, it needs the real game window.
    [SupportedOSPlatform("windows")]
    private static CheckpointScreenshot MakeCheckpointTaker(GameProcess game, string testName, string testDirectory) =>
        async (fileLabel, _) =>
        {
            IntPtr window = await WindowCapture.WaitForWindowAsync(game, TimeSpan.FromSeconds(2));
            string fileName = $"{testName}_{fileLabel}.png";
            await WindowCapture.CaptureClientAreaAsync(window, Path.Combine(testDirectory, fileName));
            return $"{testName}/{fileName}";
        };

    private static TestResult Failure(TestDefinition test, TestOutcome outcome, string reason, Stopwatch clock) =>
        new()
        {
            Name = test.Name,
            Outcome = outcome,
            Duration = clock.Elapsed,
            FailureReason = reason,
            Log = new[] { reason },
            ScreenshotError = "the game did not start, so there was no window to capture",
        };
}






