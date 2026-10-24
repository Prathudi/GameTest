using Xunit;

namespace GameTest.Runner.Tests;

/// <summary>
/// A tiny stand-in for the real game. Enter starts it, Right/Left move the player within
/// x = 1..3, Escape pauses and resumes. Reaching x = 3 collects the one coin.
/// </summary>
internal sealed class FakeGameClient : IGameClient
{
    private string _scene = "main_menu";
    private int _x = 1;
    private int _score;
    private int _coins = 1;

    /// <summary>When true, Enter does nothing (simulates a game that never starts playing).</summary>
    public bool IgnoreEnter { get; init; }

    public Exception? ThrowOnGetState { get; init; }
    public Exception? ThrowOnPress { get; init; }

    public List<string> Pressed { get; } = new();

    public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PressKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnPress is not null) throw ThrowOnPress;
        Pressed.Add(key);

        switch (_scene)
        {
            case "main_menu":
                if (key == "Enter" && !IgnoreEnter) _scene = "playing";
                break;
            case "playing":
                if (key == "Escape") _scene = "paused";
                else if (key == "Right" && _x < 3) { _x++; CollectCoin(); }
                else if (key == "Left" && _x > 1) _x--;
                break;
            case "paused":
                if (key == "Escape") _scene = "playing";
                break;
        }
        return Task.CompletedTask;
    }

    public Task<GameState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnGetState is not null) throw ThrowOnGetState;
        return Task.FromResult(new GameState(_scene, new Point(_x, 1), _score, _coins, 5, 3));
    }

    public Task QuitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private void CollectCoin()
    {
        if (_x == 3 && _coins > 0)
        {
            _score++;
            _coins--;
        }
    }
}

/// <summary>Short builders so the tests read like the JSON they stand for.</summary>
internal static class Build
{
    public static TestStep Press(string key) => new() { Action = "press", Key = key };

    public static TestStep PressMany(params string[] keys) => new() { Action = "press", Keys = keys.ToList() };

    public static TestStep AssertState(StateExpectation expect) => new() { Action = "assert", Expect = expect };

    public static TestStep WaitFor(StateExpectation expect, int timeoutMs) =>
        new() { Action = "wait_for", Expect = expect, TimeoutMs = timeoutMs };

    public static TestStep Screenshot(string label) => new() { Action = "screenshot", Label = label };

    public static TestDefinition Test(int timeoutMs, params TestStep[] steps) =>
        new() { Name = "t", TimeoutMs = timeoutMs, Steps = steps.ToList() };

    public static StateExpectation Scene(string scene) => new() { Scene = scene };
}

/// <summary>A folder in the system temp directory that is deleted when the test ends.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Folder { get; } =
        Path.Combine(Path.GetTempPath(), "gametest_tests_" + Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Folder);

    public string Write(string fileName, string content)
    {
        string path = Path.Combine(Folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { }
    }
}
