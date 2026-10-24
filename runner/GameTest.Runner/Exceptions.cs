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

/// <summary>A test file cannot be read or does not follow the test format.</summary>
public class TestDefinitionException : Exception
{
    public TestDefinitionException(string message) : base(message) { }
}


/// <summary>A screenshot could not be taken.</summary>
public class ScreenshotException : Exception
{
    public ScreenshotException(string message) : base(message) { }
}
