using System.Diagnostics;

namespace GameTest.Runner;

/// <summary>The "run-all" command: run every test in a folder, print a summary, write a report.</summary>
public static class RunAllCommand
{
    private const string OutputRoot = "results";
    private const int InterruptedExitCode = 130;  // the usual exit code for "stopped with Ctrl+C"

    // What one pass over the suite produced.
    private sealed record SuiteRun(
        List<TestResult> Completed, bool Interrupted, string RunDirectory, DateTimeOffset StartedAt, TimeSpan Duration);

    /// <returns>0 = all passed, 1 = at least one test did not pass, 2 = setup problem, 130 = interrupted.</returns>
    /// <remarks>Usage: run-all [folder] [game.exe] [--repeat N]</remarks>
    public static async Task<int> RunAsync(string[] args, Func<string?> findGame)
    {
        if (!TryParseArgs(args, out List<string> positional, out int repeats, out string? argError))
        {
            Console.WriteLine($"ERROR  {argError}");
            return 2;
        }

        string folder = positional.Count > 0 ? positional[0] : Path.Combine("tests", "json");
        string? gameExe = positional.Count > 1 ? positional[1] : findGame();
        if (gameExe is null)
        {
            Console.WriteLine("ERROR  could not find the game. Build it first, or pass its path as the second argument.");
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

        WarnAboutRunningGames(gameExe);

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

        try
        {
            return repeats == 1
                ? await RunOnceAsync(loaded.Tests, folder, gameExe, cancellation.Token)
                : await RunRepeatedAsync(loaded.Tests, folder, gameExe, repeats, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    // ---- a single run ------------------------------------------------------------------

    private static async Task<int> RunOnceAsync(
        IReadOnlyList<TestDefinition> tests, string folder, string gameExe, CancellationToken token)
    {
        SuiteRun? run = await RunSuiteAsync(tests, folder, gameExe, OutputRoot, label: null, token);
        if (run is null) return 2;

        if (run.Interrupted)
        {
            Console.WriteLine($"\nINTERRUPTED  {run.Completed.Count} of {tests.Count} tests finished; the rest were not run.");
        }
        ConsoleSummary.Print(run.Completed, run.Duration, run.RunDirectory);

        if (!TryWriteReport(run, folder, gameExe)) return 2;

        if (run.Interrupted) return InterruptedExitCode;
        return run.Completed.All(r => r.Outcome == TestOutcome.Pass) ? 0 : 1;
    }

    // ---- several runs ------------------------------------------------------------------

    private static async Task<int> RunRepeatedAsync(
        IReadOnlyList<TestDefinition> tests, string folder, string gameExe, int repeats, CancellationToken token)
    {
        string repeatFolder = Path.Combine(OutputRoot, "repeat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        var allResults = new List<IReadOnlyList<TestResult>>();
        bool interrupted = false;

        for (int i = 1; i <= repeats && !interrupted; i++)
        {
            Console.WriteLine($"==== Repeat {i} of {repeats} ====");
            SuiteRun? run = await RunSuiteAsync(tests, folder, gameExe, repeatFolder, $"repeat_{i:D2}", token);
            if (run is null) return 2;

            // An interrupted repeat is only partly done, so it is left out of the statistics.
            if (run.Interrupted) interrupted = true;
            else allResults.Add(run.Completed);

            ConsoleSummary.Print(run.Completed, run.Duration, run.RunDirectory);
            if (!TryWriteReport(run, folder, gameExe)) return 2;
            Console.WriteLine();
        }

        if (allResults.Count == 0)
        {
            Console.WriteLine("INTERRUPTED  before the first repeat finished: there are no statistics.");
            return InterruptedExitCode;
        }

        var summary = new RepeatSummary(
            repeats, allResults.Count, interrupted, folder, StabilityCalculator.Calculate(allResults));
        RepeatSummaryWriter.Print(summary);
        try
        {
            Console.WriteLine($"\nRepeat summary: {RepeatSummaryWriter.Write(repeatFolder, summary)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"\nERROR  could not write the repeat summary: {ex.Message}");
            return 2;
        }

        if (interrupted) return InterruptedExitCode;
        return summary.AllPassedEveryTime ? 0 : 1;
    }

    // ---- shared pieces -----------------------------------------------------------------

    // Runs the suite once into a new folder under outputRoot. Returns null if the folder cannot be made.
    private static async Task<SuiteRun?> RunSuiteAsync(
        IReadOnlyList<TestDefinition> tests,
        string folder,
        string gameExe,
        string outputRoot,
        string? label,
        CancellationToken token)
    {
        DateTimeOffset startedAt = DateTimeOffset.Now;
        string runDirectory;
        try
        {
            runDirectory = label is null
                ? ReportWriter.CreateRunDirectory(outputRoot, startedAt)
                : Directory.CreateDirectory(Path.Combine(outputRoot, label)).FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"ERROR  could not create the output folder: {ex.Message}");
            return null;
        }

        Console.WriteLine($"Game:   {gameExe}");
        Console.WriteLine($"Tests:  {tests.Count} from {folder}");
        Console.WriteLine($"Output: {Path.GetFullPath(runDirectory)}\n");

        int count = tests.Count;
        var completed = new List<TestResult>();
        bool interrupted = false;
        var clock = Stopwatch.StartNew();
        try
        {
            await TestRunner.RunAllAsync(tests, gameExe, runDirectory, result =>
            {
                completed.Add(result);
                Console.WriteLine($"[{completed.Count}/{count}] {result.Outcome.ToLabel(),-8}{result.Name}  ({result.Duration.TotalMilliseconds:0} ms)");
            }, token);
        }
        catch (OperationCanceledException)
        {
            interrupted = true;
        }
        clock.Stop();

        return new SuiteRun(completed, interrupted, runDirectory, startedAt, clock.Elapsed);
    }

    private static bool TryWriteReport(SuiteRun run, string folder, string gameExe)
    {
        try
        {
            string reportPath = ReportWriter.Write(
                run.RunDirectory,
                new RunInfo(run.StartedAt, run.Duration, gameExe, folder, run.Interrupted),
                run.Completed);
            Console.WriteLine($"\nReport: {reportPath}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"\nERROR  could not write the report: {ex.Message}");
            return false;
        }
    }

    // Splits "folder game.exe --repeat N" into positional arguments and the repeat count.
    private static bool TryParseArgs(string[] args, out List<string> positional, out int repeats, out string? error)
    {
        positional = new List<string>();
        repeats = 1;
        error = null;

        for (int i = 1; i < args.Length; i++)  // args[0] is "run-all"
        {
            if (args[i] == "--repeat")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out repeats) || repeats < 1 || repeats > 1000)
                {
                    error = "--repeat needs a whole number from 1 to 1000";
                    return false;
                }
                i++;
            }
            else if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"unknown option '{args[i]}' (the only option is --repeat N)";
                return false;
            }
            else
            {
                positional.Add(args[i]);
            }
        }
        return true;
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
