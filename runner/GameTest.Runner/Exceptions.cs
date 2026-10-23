namespace GameTest.Runner;

/// <summary>The game answered, but said the request failed ({"ok":false,...}).</summary>
public class AgentException : Exception
{
    public AgentException(string message) : base(message) { }
}

/// <summary>The game (or its agent) did not answer in time.</summary>
public class AgentTimeoutException : Exception
{
    public AgentTimeoutException(string message) : base(message) { }
}

/// <summary>The game process ended when it should still have been running.</summary>
public class GameCrashedException : Exception
{
    public int? ExitCode { get; }

    public GameCrashedException(string message, int? exitCode = null) : base(message)
    {
        ExitCode = exitCode;
    }
}