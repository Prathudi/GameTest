using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GameTest.Runner;

/// <summary>
/// Talks to the game's test agent over TCP: one JSON request per line, one
/// JSON reply per line. Use one request at a time (it is not thread-safe).
/// </summary>
public sealed class AgentClient : IGameClient, IDisposable
{
    // The game uses snake_case names ("coins_remaining"); C# uses PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly TcpClient _tcp;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private bool _broken;

    /// <summary>How long to wait for the game to answer one request.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    private AgentClient(TcpClient tcp)
    {
        _tcp = tcp;
        var stream = tcp.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
    }

    public static async Task<AgentClient> ConnectAsync(int port, TimeSpan timeout)
    {
        var tcp = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await tcp.ConnectAsync("127.0.0.1", port, cts.Token);
        }
        catch (OperationCanceledException)
        {
            tcp.Dispose();
            throw new AgentTimeoutException(
                $"could not connect to the agent on port {port} within {timeout.TotalSeconds:0.#} s");
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
        return new AgentClient(tcp);
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(new { cmd = "ping" }, cancellationToken);
    }

    public async Task PressKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await SendAsync(new { cmd = "key", key }, cancellationToken);
    }

    public async Task<GameState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        JsonElement reply = await SendAsync(new { cmd = "get_state" }, cancellationToken);
        return reply.GetProperty("state").Deserialize<GameState>(JsonOptions)
               ?? throw new AgentException("the game sent an empty state");
    }

    public async Task QuitAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(new { cmd = "quit" }, cancellationToken);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _reader.Dispose();
        _tcp.Dispose();
    }

    // Sends one request and returns the reply. Throws AgentException if the game
    // says {"ok":false}, and AgentTimeoutException if it does not answer in time.
    private async Task<JsonElement> SendAsync(object request, CancellationToken cancellationToken)
    {
        if (_broken)
        {
            throw new InvalidOperationException(
                "this connection failed earlier and can no longer be used; connect again");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        string? line;
        try
        {
            await _writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
            line = await _reader.ReadLineAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A late reply could be mistaken for the answer to the NEXT request,
            // so a connection that timed out is never used again.
            _broken = true;
            throw new AgentTimeoutException(
                $"no answer from the game within {RequestTimeout.TotalSeconds:0.#} s");
        }
        catch (IOException ex)
        {
            _broken = true;
            throw new AgentException($"the connection to the game was lost (it may have crashed): {ex.Message}");
        }

        if (line is null)
        {
            _broken = true;
            throw new AgentException("the game closed the connection (it may have crashed)");
        }

        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
        {
            string error = root.TryGetProperty("error", out JsonElement e)
                ? e.GetString() ?? "unknown error"
                : "unknown error";
            throw new AgentException(error);
        }
        return root.Clone();  // keep the data after the document is disposed
    }
}