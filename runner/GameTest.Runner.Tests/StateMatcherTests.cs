using Xunit;

namespace GameTest.Runner.Tests;

public class StateMatcherTests
{
    private static readonly GameState Playing = new("playing", new Point(2, 1), 1, 2, 12, 9);

    [Fact]
    public void All_fields_equal_gives_no_mismatches()
    {
        var expect = new StateExpectation
        {
            Scene = "playing",
            Player = new ExpectedPoint { X = 2, Y = 1 },
            Score = 1,
            CoinsRemaining = 2,
        };
        Assert.Empty(StateMatcher.Compare(expect, Playing));
    }

    [Fact]
    public void Every_difference_is_reported()
    {
        var expect = new StateExpectation { Scene = "paused", Score = 5 };
        List<string> mismatches = StateMatcher.Compare(expect, Playing);

        Assert.Equal(2, mismatches.Count);
        Assert.Contains("scene expected 'paused' but was 'playing'", mismatches);
        Assert.Contains("score expected 5 but was 1", mismatches);
    }

    [Fact]
    public void Player_with_only_x_does_not_check_y()
    {
        var expect = new StateExpectation { Player = new ExpectedPoint { X = 2 } };
        Assert.Empty(StateMatcher.Compare(expect, Playing));
    }

    [Fact]
    public void Wrong_player_y_is_reported()
    {
        var expect = new StateExpectation { Player = new ExpectedPoint { Y = 7 } };
        Assert.Contains("player.y expected 7 but was 1", StateMatcher.Compare(expect, Playing));
    }

    [Fact]
    public void An_empty_expectation_has_nothing_to_compare()
    {
        Assert.Empty(StateMatcher.Compare(new StateExpectation(), Playing));
    }

    [Fact]
    public void Describe_lists_only_the_fields_that_are_set()
    {
        var expect = new StateExpectation { Scene = "playing", Score = 2 };
        Assert.Equal("scene=playing score=2", StateMatcher.Describe(expect));
    }
}
