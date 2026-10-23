namespace GameTest.Runner;

/// <summary>
/// What a test needs from the game. Tests talk to this interface, not to TCP,
/// so a fake game can stand in for the real one in unit tests.
/// </summary>
public interface IGameClient
{
    Task PingAsync(CancellationToken cancellationToken = default);

    /// <summary>Presses a key by name: Up, Down, Left, Right, Enter or Escape.</summary>
    Task PressKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<GameState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the game to close its window.</summary>
    Task QuitAsync(CancellationToken cancellationToken = default);
}