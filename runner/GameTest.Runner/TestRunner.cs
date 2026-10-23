using System.Diagnostics;

namespace GameTest.Runner;

/// <summary>Runs many tests, each on its own freshly started game.</summary>
public static class TestRunner
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs the tests one after another. A failing test never stops the others.
    /// <paramref name="onResult"/> is called as soon as each test finishes.
    /// </summary>
    public static async Task<List<TestResult>> RunAllAsync(
        IReadOnlyList<TestDefinition> tests,
        string gameExe,
        Action<TestResult>? onResult = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        foreach (TestDefinition test in tests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestResult result = await RunOneAsync(test, gameExe, cancellationToken);
            results.Add(result);
            onResult?.Invoke(result);
        }
        return results;
    }

    /// <summary>Starts a game, runs one test on it, and always shuts the game down.</summary>
    public static async Task<TestResult> RunOneAsync(
        TestDefinition test, string gameExe, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        GameProcess? game = null;
        try
        {
            game = await GameProcess.StartAsync(gameExe, StartupTimeout);
            using AgentClient client = await game.ConnectAsync(StartupTimeout);

            TestResult result = await TestExecutor.RunAsync(test, client, cancellationToken);
            if (result.Outcome != TestOutcome.Pass) result = await ExplainIfGameDiedAsync(result, game);

            await game.StopAsync(client);
            return result;
        }
        catch (AgentTimeoutException ex)
        {
            return Failure(test, TestOutcome.Timeout, $"the game could not be started: {ex.Message}", clock);
        }
        catch (GameCrashedException ex)
        {
            return Failure(test, TestOutcome.Fail, $"the game crashed while starting: {ex.Message}", clock);
        }
        catch (FileNotFoundException ex)
        {
            return Failure(test, TestOutcome.Fail, ex.Message, clock);
        }
        catch (AgentException ex)
        {
            return Failure(test, TestOutcome.Fail, $"the game could not be started: {ex.Message}", clock);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bug or an odd environment problem in ONE test must not end the whole run.
            return Failure(test, TestOutcome.Fail, $"unexpected error: {ex.GetType().Name}: {ex.Message}", clock);
        }
        finally
        {
            if (game is not null) await game.DisposeAsync();
        }
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
            Log = log,
        };
    }

    private static TestResult Failure(TestDefinition test, TestOutcome outcome, string reason, Stopwatch clock) =>
        new()
        {
            Name = test.Name,
            Outcome = outcome,
            Duration = clock.Elapsed,
            FailureReason = reason,
            Log = new[] { reason },
        };
}
