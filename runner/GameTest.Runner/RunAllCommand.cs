using System.Diagnostics;

namespace GameTest.Runner;

/// <summary>The "run-all" command: run every test in a folder, print a summary, write a report.</summary>
public static class RunAllCommand
{
    private const string OutputRoot = "results";
    private const int InterruptedExitCode = 130;  // the usual exit code for "stopped with Ctrl+C"

    /// <returns>0 = all passed, 1 = at least one test did not pass, 2 = setup problem, 130 = interrupted.</returns>
    public static async Task<int> RunAsync(string[] args, Func<string?> findGame)
    {
        string folder = args.Length > 1 ? args[1] : Path.Combine("tests", "json");
        string? gameExe = args.Length > 2 ? args[2] : findGame();
        if (gameExe is null)
        {
            Console.WriteLine("ERROR  could not find the game. Build it first, or pass its path as the third argument.");
            return 2;
        }

        LoadResult loaded = TestLoader.LoadDirectory(folder);
        if (loaded.Errors.Count > 0)
        {
            foreach (string error in loaded.Errors) Console.WriteLine($"ERROR  {error}");
            Console.WriteLine("\nNo tests were run: fix the test files first.");
            return 2;
        }

        // Screenshots must show real screen pixels, so this program has to know the display scaling.
        if (OperatingSystem.IsWindows()) WindowCapture.MakeProcessDpiAware();

        // The tests write their evidence into this folder, so it must exist before they start.
        DateTimeOffset startedAt = DateTimeOffset.Now;
        string runDirectory;
        try
        {
            runDirectory = ReportWriter.CreateRunDirectory(OutputRoot, startedAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"ERROR  could not create the output folder: {ex.Message}");
            return 2;
        }

        WarnAboutRunningGames(gameExe);

        Console.WriteLine($"Game:   {gameExe}");
        Console.WriteLine($"Tests:  {loaded.Tests.Count} from {folder}");
        Console.WriteLine($"Output: {Path.GetFullPath(runDirectory)}\n");

        int count = loaded.Tests.Count;
        var completed = new List<TestResult>();
        bool interrupted = false;

        // First Ctrl+C: stop cleanly (the running game is closed, finished results are kept).
        // Second Ctrl+C: let the system end the program at once.
        using var cancellation = new CancellationTokenSource();
        int presses = 0;
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            if (++presses > 1) return;
            e.Cancel = true;
            Console.WriteLine("\nCtrl+C: stopping. The current game is being closed (press Ctrl+C again to quit at once).");
            cancellation.Cancel();
        };
        Console.CancelKeyPress += onCancel;

        var clock = Stopwatch.StartNew();
        try
        {
            await TestRunner.RunAllAsync(loaded.Tests, gameExe, runDirectory, result =>
            {
                completed.Add(result);
                Console.WriteLine($"[{completed.Count}/{count}] {result.Outcome.ToLabel(),-8}{result.Name}  ({result.Duration.TotalMilliseconds:0} ms)");
            }, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            interrupted = true;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
        clock.Stop();

        if (interrupted)
        {
            Console.WriteLine($"\nINTERRUPTED  {completed.Count} of {count} tests finished; the rest were not run.");
        }

        ConsoleSummary.Print(completed, clock.Elapsed, runDirectory);

        try
        {
            string reportPath = ReportWriter.Write(
                runDirectory, new RunInfo(startedAt, clock.Elapsed, gameExe, folder, interrupted), completed);
            Console.WriteLine($"\nReport: {reportPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"\nERROR  could not write the report: {ex.Message}");
            return 2;
        }

        if (interrupted) return InterruptedExitCode;
        return completed.All(r => r.Outcome == TestOutcome.Pass) ? 0 : 1;
    }

    // Old game processes do no harm to the port (the game picks a free one), but they can leak
    // memory and clutter the screen, which makes problems harder to see. Warn, don't stop.
    private static void WarnAboutRunningGames(string gameExe)
    {
        string processName = Path.GetFileNameWithoutExtension(gameExe);
        Process[] running = Process.GetProcessesByName(processName);
        int count = running.Length;
        foreach (Process process in running) process.Dispose();

        if (count > 0)
        {
            Console.WriteLine($"WARNING  {count} '{processName}' process(es) were already running before this run.");
            Console.WriteLine("         Close them for clean results: old windows can confuse screenshots.\n");
        }
    }
}
