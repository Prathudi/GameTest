using Xunit;

namespace GameTest.Runner.Tests;

public class TestExecutorTests
{
    [Fact]
    public async Task A_correct_sequence_passes()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000,
            Build.AssertState(Build.Scene("main_menu")),
            Build.Press("Enter"),
            Build.WaitFor(Build.Scene("playing"), 1000),
            Build.Press("Right"),
            Build.AssertState(new StateExpectation { Player = new ExpectedPoint { X = 2, Y = 1 } }));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Pass, result.Outcome);
        Assert.Null(result.FailureReason);
        Assert.Null(result.FailedStep);
    }

    [Fact]
    public async Task A_wrong_value_is_a_fail_naming_the_step_and_the_difference()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000,
            Build.Press("Enter"),
            Build.AssertState(new StateExpectation { Score = 5 }));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Fail, result.Outcome);
        Assert.Equal(2, result.FailedStep);
        Assert.Contains("score expected 5 but was 0", result.FailureReason);
    }

    [Fact]
    public async Task Steps_after_a_failure_are_not_run()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000,
            Build.Press("Enter"),
            Build.AssertState(new StateExpectation { Score = 5 }),
            Build.Press("Right"));

        await TestExecutor.RunAsync(test, game);

        Assert.Equal(new[] { "Enter" }, game.Pressed.ToArray());
    }

    [Fact]
    public async Task A_wall_stops_the_player()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000,
            Build.Press("Enter"),
            Build.PressMany("Right", "Right", "Right", "Right", "Right"),
            Build.AssertState(new StateExpectation { Player = new ExpectedPoint { X = 3 }, Score = 1, CoinsRemaining = 0 }));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Pass, result.Outcome);
    }

    [Fact]
    public async Task A_wait_for_that_never_matches_is_a_timeout_with_the_last_state()
    {
        var game = new FakeGameClient { IgnoreEnter = true };
        TestDefinition test = Build.Test(10_000,
            Build.Press("Enter"),
            Build.WaitFor(Build.Scene("playing"), 300));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Timeout, result.Outcome);
        Assert.Equal(2, result.FailedStep);
        Assert.Contains("wait_for timed out after 300 ms", result.FailureReason);
        Assert.Contains("scene=main_menu", result.FailureReason);
        Assert.True(result.Duration < TimeSpan.FromSeconds(3), "the wait should end near its own limit");
    }

    [Fact]
    public async Task The_whole_test_has_a_time_limit()
    {
        var game = new FakeGameClient();
        var slow = new TestStep { Action = "press", Key = "Enter", DelayMs = 5000 };
        TestDefinition test = Build.Test(200, slow);

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Timeout, result.Outcome);
        Assert.Contains("did not finish within 200 ms", result.FailureReason);
        Assert.True(result.Duration < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task An_error_reply_from_the_game_is_a_fail()
    {
        var game = new FakeGameClient { ThrowOnPress = new AgentException("unknown key") };
        TestDefinition test = Build.Test(5000, Build.Press("Enter"));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Fail, result.Outcome);
        Assert.Contains("unknown key", result.FailureReason);
    }

    [Fact]
    public async Task A_game_that_stops_answering_is_a_timeout()
    {
        var game = new FakeGameClient { ThrowOnGetState = new AgentTimeoutException("no answer from the game within 5 s") };
        TestDefinition test = Build.Test(5000, Build.AssertState(Build.Scene("main_menu")));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Timeout, result.Outcome);
        Assert.Contains("no answer from the game", result.FailureReason);
    }

    [Fact]
    public async Task Cancelling_the_run_throws_instead_of_reporting_a_result()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000, Build.Press("Enter"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => TestExecutor.RunAsync(test, game, cancellation.Token));
    }

    [Fact]
    public async Task A_screenshot_step_is_skipped_when_no_pictures_are_being_saved()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000, Build.Screenshot("start"));

        TestResult result = await TestExecutor.RunAsync(test, game);

        Assert.Equal(TestOutcome.Pass, result.Outcome);
        Assert.Empty(result.Checkpoints);
        Assert.Contains(result.Log, line => line.Contains("skipped"));
    }

    [Fact]
    public async Task A_saved_screenshot_is_recorded_with_its_step_number()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000, Build.Press("Enter"), Build.Screenshot("start"));
        CheckpointScreenshot taker = (label, _) => Task.FromResult($"t/{label}.png");

        TestResult result = await TestExecutor.RunAsync(test, game, takeScreenshot: taker);

        Assert.Equal(TestOutcome.Pass, result.Outcome);
        Assert.Equal(new[] { "t/step2_start.png" }, result.Checkpoints.ToArray());
    }

    [Fact]
    public async Task A_screenshot_that_cannot_be_taken_does_not_fail_the_test()
    {
        var game = new FakeGameClient();
        TestDefinition test = Build.Test(5000, Build.Screenshot("start"), Build.Press("Enter"));
        CheckpointScreenshot taker = (_, _) => throw new ScreenshotException("no window");

        TestResult result = await TestExecutor.RunAsync(test, game, takeScreenshot: taker);

        Assert.Equal(TestOutcome.Pass, result.Outcome);
        Assert.Empty(result.Checkpoints);
        Assert.Contains(result.Log, line => line.Contains("FAILED") && line.Contains("no window"));
        Assert.Equal(new[] { "Enter" }, game.Pressed.ToArray());
    }
}
