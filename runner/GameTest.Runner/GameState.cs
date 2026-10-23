namespace GameTest.Runner;

/// <summary>A cell position on the game grid.</summary>
public sealed record Point(int X, int Y);

/// <summary>What the game reports for {"cmd":"get_state"}.</summary>
public sealed record GameState(
    string Scene,
    Point Player,
    int Score,
    int CoinsRemaining,
    int Width,
    int Height);