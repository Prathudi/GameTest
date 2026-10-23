namespace GameTest.Runner;

/// <summary>Compares what a test expects with what the game really reports.</summary>
public static class StateMatcher
{
    /// <summary>Returns one message per difference. An empty list means the state matches.</summary>
    public static List<string> Compare(StateExpectation expect, GameState actual)
    {
        var mismatches = new List<string>();

        if (expect.Scene is not null && expect.Scene != actual.Scene)
            mismatches.Add($"scene expected '{expect.Scene}' but was '{actual.Scene}'");
        if (expect.Player?.X is int x && x != actual.Player.X)
            mismatches.Add($"player.x expected {x} but was {actual.Player.X}");
        if (expect.Player?.Y is int y && y != actual.Player.Y)
            mismatches.Add($"player.y expected {y} but was {actual.Player.Y}");
        if (expect.Score is int score && score != actual.Score)
            mismatches.Add($"score expected {score} but was {actual.Score}");
        if (expect.CoinsRemaining is int coins && coins != actual.CoinsRemaining)
            mismatches.Add($"coins_remaining expected {coins} but was {actual.CoinsRemaining}");

        return mismatches;
    }

    public static string Describe(StateExpectation expect)
    {
        var parts = new List<string>();
        if (expect.Scene is not null) parts.Add($"scene={expect.Scene}");
        if (expect.Player?.X is int x) parts.Add($"player.x={x}");
        if (expect.Player?.Y is int y) parts.Add($"player.y={y}");
        if (expect.Score is int score) parts.Add($"score={score}");
        if (expect.CoinsRemaining is int coins) parts.Add($"coins_remaining={coins}");
        return string.Join(" ", parts);
    }

    public static string Describe(GameState state) =>
        $"scene={state.Scene} player=({state.Player.X},{state.Player.Y}) score={state.Score} coins_remaining={state.CoinsRemaining}";
}
