using GameTest.Runner;

// Usage: GameTest.Runner [path-to-game-executable]
// This first version runs a fixed smoke check. Later checkpoints replace it
// with tests read from JSON files.

// "validate [folder]": load the test files and report problems, without starting the game.
if (args.Length > 0 && args[0] == "validate")
{
    string folder = args.Length > 1 ? args[1] : Path.Combine("tests", "json");
    LoadResult loaded = TestLoader.LoadDirectory(folder);
    foreach (TestDefinition loadedTest in loaded.Tests)
    {
        Console.WriteLine($"OK     {loadedTest.Name}  ({loadedTest.Steps.Count} steps)  {Path.GetFileName(loadedTest.SourceFile)}");
    }
    foreach (string error in loaded.Errors)
    {
        Console.WriteLine($"ERROR  {error}");
    }
    Console.WriteLine($"\n{loaded.Tests.Count} valid, {loaded.Errors.Count} invalid.");
    return loaded.Errors.Count == 0 ? 0 : 1;
}
// "run <test.json> [game.exe]": run one test file against a freshly started game.
if (args.Length > 0 && args[0] == "run")
{
    if (args.Length < 2)
    {
        Console.WriteLine("usage: run <test.json> [path-to-game]");
        return 2;
    }
    string? gameExe = args.Length > 2 ? args[2] : FindGameExecutable();
    if (gameExe is null)
    {
        Console.WriteLine("ERROR  could not find the game. Build it first, or pass its path as the third argument.");
        return 2;
    }

    TestDefinition test;
    try
    {
        test = TestLoader.LoadFile(args[1]);
    }
    catch (TestDefinitionException ex)
    {
        Console.WriteLine($"ERROR  {ex.Message}");
        return 2;
    }

    try
    {
        await using GameProcess game = await GameProcess.StartAsync(gameExe, TimeSpan.FromSeconds(10));
        using AgentClient client = await game.ConnectAsync(TimeSpan.FromSeconds(10));
        TestResult result = await TestExecutor.RunAsync(test, client);
        foreach (string line in result.Log) Console.WriteLine(line);
        Console.WriteLine($"\n{result.Outcome.ToString().ToUpperInvariant()}  {result.Name}  ({result.Duration.TotalMilliseconds:0} ms)");
        if (result.FailureReason is not null) Console.WriteLine($"       {result.FailureReason}");
        await game.StopAsync(client);
        return result.Outcome == TestOutcome.Pass ? 0 : 1;
    }
    catch (Exception ex) when (ex is AgentTimeoutException or AgentException or GameCrashedException or FileNotFoundException)
    {
        Console.WriteLine($"ERROR  {ex.Message}");
        return 2;
    }
}
// "run-all [folder] [game.exe]": run every test in a folder, each on a fresh game, and write a report.
if (args.Length > 0 && args[0] == "run-all")
{
    return await RunAllCommand.RunAsync(args, FindGameExecutable);
}
string? exe = args.Length > 0 ? args[0] : FindGameExecutable();
if (exe is null)
{
    Console.WriteLine("ERROR  could not find the game. Build it first, or pass its path as an argument.");
    return 2;
}
Console.WriteLine($"Game: {exe}");

int failures = 0;

void Check(bool passed, string description)
{
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {description}");
    if (!passed) failures++;
}

try
{
    await using GameProcess game = await GameProcess.StartAsync(exe, TimeSpan.FromSeconds(10));
    Console.WriteLine($"Game started; its agent listens on port {game.AgentPort}");
    using AgentClient client = await game.ConnectAsync(TimeSpan.FromSeconds(10));

    await client.PingAsync();
    Check(true, "the agent answers a ping");

    GameState menu = await client.GetStateAsync();
    Check(menu.Scene == "main_menu", "the game starts in the main menu");

    await client.PressKeyAsync("Enter");
    GameState playing = await client.GetStateAsync();
    Check(playing.Scene == "playing", "Enter starts the game");

    await client.PressKeyAsync("Right");
    GameState moved = await client.GetStateAsync();
    Check(moved.Player == new Point(playing.Player.X + 1, playing.Player.Y),
          "Right moves the player one cell to the right");

    await client.PressKeyAsync("Up");
    GameState blocked = await client.GetStateAsync();
    Check(blocked.Player == moved.Player, "walking into a wall does not move the player");

    await client.PressKeyAsync("Escape");
    GameState paused = await client.GetStateAsync();
    Check(paused.Scene == "paused", "Escape pauses the game");

    await client.PressKeyAsync("Right");
    GameState stillPaused = await client.GetStateAsync();
    Check(stillPaused.Player == paused.Player, "the player cannot move while paused");

    await client.PressKeyAsync("Escape");
    GameState resumed = await client.GetStateAsync();
    Check(resumed.Scene == "playing" && resumed.Player == paused.Player,
          "Escape resumes the game where it was");

    await game.StopAsync(client);
    Check(game.ExitCode == 0, "the game exits cleanly after a quit request");
}
catch (Exception ex) when (ex is AgentTimeoutException or AgentException or GameCrashedException or FileNotFoundException)
{
    Console.WriteLine($"ERROR  {ex.Message}");
    return 2;
}

Console.WriteLine(failures == 0 ? "\nAll checks passed." : $"\n{failures} check(s) failed.");
return failures == 0 ? 0 : 1;

// Looks for a built game next to this project, walking up from the program's folder.
static string? FindGameExecutable()
{
    string[] candidates =
    {
        Path.Combine("build", "game", "Release", "game.exe"),
        Path.Combine("build", "game", "game.exe"),
        Path.Combine("build", "game", "game"),
    };
    for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        foreach (string candidate in candidates)
        {
            string path = Path.Combine(dir.FullName, candidate);
            if (File.Exists(path)) return path;
        }
    }
    return null;
}



