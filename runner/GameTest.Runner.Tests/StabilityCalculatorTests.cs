using Xunit;

namespace GameTest.Runner.Tests;

public class StabilityCalculatorTests
{
    private static TestResult Result(string name, TestOutcome outcome) => new()
    {
        Name = name,
        Outcome = outcome,
        Duration = TimeSpan.FromMilliseconds(10),
        Log = new List<string>(),
    };

    // One repeat of the suite: a list of (test name, outcome) pairs.
    private static IReadOnlyList<TestResult> Repeat(params (string Name, TestOutcome Outcome)[] results) =>
        results.Select(r => Result(r.Name, r.Outcome)).ToList();

    [Fact]
    public void A_test_that_passes_then_fails_then_passes_is_flaky()
    {
        var repeats = new[]
        {
            Repeat(("t", TestOutcome.Pass)),
            Repeat(("t", TestOutcome.Fail)),
            Repeat(("t", TestOutcome.Pass)),
        };

        TestStability s = Assert.Single(StabilityCalculator.Calculate(repeats));

        Assert.True(s.Flaky);
        Assert.Equal(3, s.Runs);
        Assert.Equal(2, s.Passed);
        Assert.Equal(1, s.Failed);
        Assert.Equal(0, s.TimedOut);
        Assert.Equal(new[] { "PASS", "FAIL", "PASS" }, s.Outcomes.ToArray());
    }

    [Fact]
    public void A_test_that_always_passes_is_stable()
    {
        var repeats = new[]
        {
            Repeat(("t", TestOutcome.Pass)),
            Repeat(("t", TestOutcome.Pass)),
        };

        TestStability s = Assert.Single(StabilityCalculator.Calculate(repeats));

        Assert.False(s.Flaky);
        Assert.Equal(s.Runs, s.Passed);
    }

    [Fact]
    public void A_test_that_always_fails_is_not_flaky()
    {
        var repeats = new[]
        {
            Repeat(("t", TestOutcome.Fail)),
            Repeat(("t", TestOutcome.Fail)),
            Repeat(("t", TestOutcome.Fail)),
        };

        TestStability s = Assert.Single(StabilityCalculator.Calculate(repeats));

        Assert.False(s.Flaky);
        Assert.Equal(0, s.Passed);
        Assert.Equal(3, s.Failed);
    }

    [Fact]
    public void A_test_that_fails_in_different_ways_but_never_passes_is_not_flaky()
    {
        var repeats = new[]
        {
            Repeat(("t", TestOutcome.Fail)),
            Repeat(("t", TestOutcome.Timeout)),
            Repeat(("t", TestOutcome.Fail)),
        };

        TestStability s = Assert.Single(StabilityCalculator.Calculate(repeats));

        Assert.False(s.Flaky);
        Assert.Equal(0, s.Passed);
        Assert.Equal(2, s.Failed);
        Assert.Equal(1, s.TimedOut);
    }

    [Fact]
    public void A_test_that_passes_once_and_times_out_once_is_flaky()
    {
        var repeats = new[]
        {
            Repeat(("t", TestOutcome.Pass)),
            Repeat(("t", TestOutcome.Timeout)),
        };

        TestStability s = Assert.Single(StabilityCalculator.Calculate(repeats));

        Assert.True(s.Flaky);
        Assert.Equal(1, s.Passed);
        Assert.Equal(1, s.TimedOut);
    }

    [Fact]
    public void Tests_keep_their_first_seen_order_and_a_missing_test_just_has_fewer_runs()
    {
        var repeats = new[]
        {
            Repeat(("a", TestOutcome.Pass), ("b", TestOutcome.Pass)),
            Repeat(("b", TestOutcome.Pass), ("c", TestOutcome.Pass)),
        };

        List<TestStability> result = StabilityCalculator.Calculate(repeats);

        Assert.Equal(new[] { "a", "b", "c" }, result.Select(s => s.Name).ToArray());
        Assert.Equal(new[] { 1, 2, 1 }, result.Select(s => s.Runs).ToArray());
    }

    [Fact]
    public void No_repeats_gives_no_statistics()
    {
        Assert.Empty(StabilityCalculator.Calculate(Array.Empty<IReadOnlyList<TestResult>>()));
    }

    [Fact]
    public void The_summary_counts_flaky_tests_and_knows_whether_everything_always_passed()
    {
        var allGood = new RepeatSummary(2, 2, false, "tests",
            new[] { new TestStability("a", 2, 2, 0, 0, false, new[] { "PASS", "PASS" }) });
        var withFlaky = new RepeatSummary(2, 2, false, "tests", new[]
        {
            new TestStability("a", 2, 2, 0, 0, false, new[] { "PASS", "PASS" }),
            new TestStability("b", 2, 1, 1, 0, true, new[] { "PASS", "FAIL" }),
        });

        Assert.True(allGood.AllPassedEveryTime);
        Assert.Equal(0, allGood.FlakyCount);
        Assert.False(withFlaky.AllPassedEveryTime);
        Assert.Equal(1, withFlaky.FlakyCount);
    }
}
