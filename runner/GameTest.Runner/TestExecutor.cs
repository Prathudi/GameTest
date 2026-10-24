using System.Diagnostics;

namespace GameTest.Runner;

/// <summary>
/// Takes a checkpoint picture of the game window. Returns the saved file's path, relative to
/// the run folder. Throws ScreenshotException if the picture cannot be taken.
/// </summary>
public delegate Task<string> CheckpointScreenshot(string fileLabel, CancellationToken cancellationToken);

/// <summary>Runs the steps of one test against a game that is already started.</summary>
public static class TestExecutor
{
    private const int DefaultWaitTimeoutMs = 5000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    // Thrown by a step to end the test with a FAIL or TIMEOUT and a reason.
    private sealed class StepFailedException : Exception
    {
        public TestOutcome Outcome { get; }

        public StepFailedException(TestOutcome outcome, string message) : base(message)
        {
            Outcome = outcome;
        }
    }

    /// <summary>
    /// Runs every step in order and stops at the first problem. Never throws for a
    /// failing test: the result says what happened. Only cancelling the given
    /// token makes it throw (OperationCanceledException). Without
    /// <paramref name="takeScreenshot"/>, "screenshot" steps are skipped.
    /// </summary>
    public static async Task<TestResult> RunAsync(
        TestDefinition test,
        IGameClient client,
        CancellationToken cancellationToken = default,
        CheckpointScreenshot? takeScreenshot = null)
    {
        var log = new List<string>();
        var checkpoints = new List<string>();
        var clock = Stopwatch.StartNew();
        void Log(string message) => log.Add($"[{clock.ElapsedMilliseconds,6} ms] {message}");

        // One time limit for the whole test, on top of any limit on a single step.
        using var testTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        testTimeout.CancelAfter(test.TimeoutMs);
        CancellationToken token = testTimeout.Token;

        TestOutcome outcome = TestOutcome.Pass;
        string? problem = null;
        int currentStep = 0;

        try
        {
            foreach (TestStep step in test.Steps)
            {
                currentStep++;
                await ExecuteStepAsync(step, currentStep, client, Log, checkpoints, takeScreenshot, token);
            }
        }
        catch (StepFailedException ex)
        {
            outcome = ex.Outcome;
            problem = ex.Message;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own time limit fired, not the caller's cancellation.
            outcome = TestOutcome.Timeout;
            problem = $"the test did not finish within {test.TimeoutMs} ms";
        }
        catch (AgentTimeoutException ex)
        {
            outcome = TestOutcome.Timeout;
            problem = ex.Message;
        }
        catch (AgentException ex)
        {
            outcome = TestOutcome.Fail;
            problem = ex.Message;
        }

        string? reason = null;
        int? failedStep = null;
        if (problem is not null)
        {
            reason = $"step {currentStep}: {problem}";
            failedStep = currentStep;
            Log($"{outcome.ToString().ToUpperInvariant()} {reason}");
        }

        return new TestResult
        {
            Name = test.Name,
            Outcome = outcome,
            Duration = clock.Elapsed,
            FailureReason = reason,
            FailedStep = failedStep,
            Log = log,
            Checkpoints = checkpoints,
        };
    }

    private static async Task ExecuteStepAsync(
        TestStep step,
        int number,
        IGameClient client,
        Action<string> log,
        List<string> checkpoints,
        CheckpointScreenshot? takeScreenshot,
        CancellationToken token)
    {
        switch (step.Action)
        {
            case "press":
            {
                List<string> keys = step.Key is not null ? new List<string> { step.Key } : step.Keys!;
                foreach (string key in keys)
                {
                    await client.PressKeyAsync(key, token);
                    log($"step {number}: pressed {key}");
                    if (step.DelayMs > 0) await Task.Delay(step.DelayMs, token);
                }
                break;
            }

            case "assert":
            {
                GameState state = await client.GetStateAsync(token);
                List<string> mismatches = StateMatcher.Compare(step.Expect!, state);
                if (mismatches.Count > 0)
                {
                    throw new StepFailedException(
                        TestOutcome.Fail, "assertion failed: " + string.Join("; ", mismatches));
                }
                log($"step {number}: assert OK ({StateMatcher.Describe(step.Expect!)})");
                break;
            }

            case "wait_for":
            {
                int timeoutMs = step.TimeoutMs ?? DefaultWaitTimeoutMs;
                var waited = Stopwatch.StartNew();
                while (true)
                {
                    GameState state = await client.GetStateAsync(token);
                    if (StateMatcher.Compare(step.Expect!, state).Count == 0)
                    {
                        log($"step {number}: wait_for OK after {waited.ElapsedMilliseconds} ms ({StateMatcher.Describe(step.Expect!)})");
                        break;
                    }
                    if (waited.ElapsedMilliseconds >= timeoutMs)
                    {
                        throw new StepFailedException(
                            TestOutcome.Timeout,
                            $"wait_for timed out after {timeoutMs} ms; wanted {StateMatcher.Describe(step.Expect!)}; last state: {StateMatcher.Describe(state)}");
                    }
                    await Task.Delay(PollInterval, token);
                }
                break;
            }

            case "screenshot":
            {
                // A checkpoint picture is evidence, not an assertion: if it cannot be taken,
                // the problem is logged and the test carries on.
                string name = step.Label ?? "checkpoint";
                if (takeScreenshot is null)
                {
                    log($"step {number}: screenshot '{name}' skipped (this run does not save screenshots)");
                    break;
                }
                try
                {
                    string file = await takeScreenshot($"step{number}_{name}", token);
                    checkpoints.Add(file);
                    log($"step {number}: screenshot saved ({file})");
                }
                catch (ScreenshotException ex)
                {
                    log($"step {number}: screenshot '{name}' FAILED: {ex.Message} (the test continues)");
                }
                break;
            }

            default:
                throw new InvalidOperationException(
                    $"unknown action '{step.Action}' (the loader should have rejected this)");
        }
    }
}
