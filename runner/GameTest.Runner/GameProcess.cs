using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameTest.Runner;

/// <summary>
/// Starts the game, waits until it is ready, and shuts it down again.
/// The game is started with "--agent-port 0", so the operating system picks a
/// free port and the game prints it ("agent listening on 127.0.0.1:PORT").
/// </summary>
public sealed class GameProcess : IAsyncDisposable
{
    private static readonly Regex ListeningLine = new(@"listening on 127\.0\.0\.1:(\d+)");

    private readonly Process _process;
    private readonly List<string> _output = new();
    private readonly object _outputLock = new();
    private bool _disposed;

    /// <summary>The port the game's agent is listening on.</summary>
    public int AgentPort { get; private set; }

    public bool HasExited => _process.HasExited;

    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    /// <summary>Everything the game has printed so far (stdout and stderr).</summary>
    public IReadOnlyList<string> Output
    {
        get
        {
            lock (_outputLock) return _output.ToArray();
        }
    }

    private GameProcess(Process process)
    {
        _process = process;
    }

    /// <summary>
    /// Launches the game and waits until it reports its agent port.
    /// Throws GameCrashedException if it exits first, or AgentTimeoutException
    /// if it stays silent for too long.
    /// </summary>
    public static async Task<GameProcess> StartAsync(string exePath, TimeSpan startupTimeout)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"game executable not found: {exePath}", exePath);
        }

        var startInfo = new ProcessStartInfo(Path.GetFullPath(exePath), "--agent-port 0")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exePath))!,
        };
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var game = new GameProcess(process);
        var portReported = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) => game.OnLine(e.Data, portReported);
        process.ErrorDataReceived += (_, e) => game.OnLine(e.Data, null);
        process.Exited += (_, _) =>
        {
            process.WaitForExit();  // lets the output readers finish, so nothing is missing below
            portReported.TrySetException(new GameCrashedException(
                $"the game exited (code {process.ExitCode}) before its agent was ready.\n{game.DescribeOutput()}",
                process.ExitCode));
        };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            game.AgentPort = await portReported.Task.WaitAsync(startupTimeout);
        }
        catch (TimeoutException)
        {
            string output = game.DescribeOutput();
            await game.DisposeAsync();
            throw new AgentTimeoutException(
                $"the game did not report its agent port within {startupTimeout.TotalSeconds:0.#} s.\n{output}");
        }
        catch
        {
            await game.DisposeAsync();
            throw;
        }
        return game;
    }

    /// <summary>
    /// Connects to the agent and waits until the game answers a ping. The agent
    /// only answers once the game's main loop is running, so a pong means ready.
    /// </summary>
    public async Task<AgentClient> ConnectAsync(TimeSpan readyTimeout)
    {
        DateTime deadline = DateTime.UtcNow + readyTimeout;
        AgentClient client = await AgentClient.ConnectAsync(AgentPort, readyTimeout);
        try
        {
            TimeSpan left = deadline - DateTime.UtcNow;
            client.RequestTimeout = left > TimeSpan.FromMilliseconds(100) ? left : TimeSpan.FromMilliseconds(100);
            await client.PingAsync();
            client.RequestTimeout = TimeSpan.FromSeconds(5);
            return client;
        }
        catch (Exception ex)
        {
            client.Dispose();
            if (_process.WaitForExit(1000))
            {
                throw new GameCrashedException(
                    $"the game exited (code {_process.ExitCode}) while starting up.\n{DescribeOutput()}",
                    _process.ExitCode);
            }
            if (ex is AgentTimeoutException)
            {
                throw new AgentTimeoutException(
                    $"the game started but did not answer a ping within {readyTimeout.TotalSeconds:0.#} s.\n{DescribeOutput()}");
            }
            throw;
        }
    }

    /// <summary>
    /// Asks the game to quit (if a client is given) and waits for it to exit.
    /// If it does not exit in time, it is killed.
    /// </summary>
    public async Task StopAsync(IGameClient? client = null, TimeSpan? grace = null)
    {
        if (_process.HasExited) return;

        if (client is not null)
        {
            try
            {
                await client.QuitAsync();
            }
            catch (Exception)
            {
                // The game may already be closing; waiting for the exit below handles that.
            }
        }

        using var cts = new CancellationTokenSource(grace ?? TimeSpan.FromSeconds(5));
        try
        {
            await _process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Kill();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_process.HasExited)
        {
            Kill();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await _process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
        _process.Dispose();
    }

    /// <summary>The game's output so far, ready to put into an error message.</summary>
    public string DescribeOutput()
    {
        string[] lines = Output.ToArray();
        return lines.Length == 0 ? "(the game printed nothing)" : "game output:\n  " + string.Join("\n  ", lines);
    }

    private void OnLine(string? line, TaskCompletionSource<int>? portReported)
    {
        if (line is null) return;
        lock (_outputLock) _output.Add(line);

        if (portReported is not null)
        {
            Match match = ListeningLine.Match(line);
            if (match.Success) portReported.TrySetResult(int.Parse(match.Groups[1].Value));
        }
    }

    private void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited by itself a moment ago.
        }
    }
}
