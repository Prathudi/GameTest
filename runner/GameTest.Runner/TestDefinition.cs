using System.Text.Json.Serialization;

namespace GameTest.Runner;

/// <summary>One test case, exactly as it is written in a JSON file.</summary>
public sealed class TestDefinition
{
    /// <summary>Short unique id; also used in screenshot and log file names.</summary>
    public string Name { get; init; } = "";

    public string Description { get; init; } = "";

    /// <summary>Time limit for the whole test, including starting the game.</summary>
    public int TimeoutMs { get; init; } = 30_000;

    public List<TestStep> Steps { get; init; } = new();

    /// <summary>The file this test came from. Set by the loader, never read from JSON.</summary>
    [JsonIgnore]
    public string SourceFile { get; set; } = "";
}

/// <summary>One action or check inside a test.</summary>
public sealed class TestStep
{
    /// <summary>"press", "wait_for" or "assert".</summary>
    public string Action { get; init; } = "";

    /// <summary>press: a single key name.</summary>
    public string? Key { get; init; }

    /// <summary>press: several keys, sent in order.</summary>
    public List<string>? Keys { get; init; }

    /// <summary>press: pause after each key, in milliseconds.</summary>
    public int DelayMs { get; init; }

    /// <summary>assert / wait_for: what the game state must look like.</summary>
    public StateExpectation? Expect { get; init; }

    /// <summary>wait_for: how long to keep polling before giving up.</summary>
    public int? TimeoutMs { get; init; }

    /// <summary>Free text for humans; ignored by the runner.</summary>
    public string? Note { get; init; }

    /// <summary>screenshot: a short name for the picture; used in the file name.</summary>
    public string? Label { get; init; }
}

/// <summary>The parts of the game state a step wants to check. Unset fields are not checked.</summary>
public sealed class StateExpectation
{
    public string? Scene { get; init; }
    public ExpectedPoint? Player { get; init; }
    public int? Score { get; init; }
    public int? CoinsRemaining { get; init; }

    [JsonIgnore]
    public bool IsEmpty => Scene is null && Player is null && Score is null && CoinsRemaining is null;
}

/// <summary>A position where x, y, or both may be checked.</summary>
public sealed class ExpectedPoint
{
    public int? X { get; init; }
    public int? Y { get; init; }
}

