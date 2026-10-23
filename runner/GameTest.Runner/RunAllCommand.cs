using System.Diagnostics;

namespace GameTest.Runner;

/// <summary>The "run-all" command: run every test in a folder, print a summary, write a report.</summary>
public static class RunAllCommand
{
    private const string OutputRoot = "results";

    /// <returns>0 = all passed, 1 = at least one test did not pass, 2 = setup problem.</returns>
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

        Console.WriteLine($"Game:  {gameExe}");
        Console.WriteLine($"Tests: {loaded.Tests.Count} from {folder}\n");

        DateTimeOffset startedAt = DateTimeOffset.Now;
        var clock = Stopwatch.StartNew();
        int done = 0;
        int count = loaded.Tests.Count;

        List<TestResult> results = await TestRunner.RunAllAsync(loaded.Tests, gameExe, result =>
        {
            done++;
            Console.WriteLine($"[{done}/{count}] {result.Outcome.ToLabel(),-8}{result.Name}  ({result.Duration.TotalMilliseconds:0} ms)");
        });
        clock.Stop();

        ConsoleSummary.Print(results, clock.Elapsed);

        try
        {
            string runDirectory = ReportWriter.CreateRunDirectory(OutputRoot, startedAt);
            string reportPath = ReportWriter.Write(
                runDirectory, new RunInfo(startedAt, clock.Elapsed, gameExe, folder), results);
            Console.WriteLine($"\nReport: {reportPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"\nERROR  could not write the report: {ex.Message}");
            return 2;
        }

        return results.All(r => r.Outcome == TestOutcome.Pass) ? 0 : 1;
    }
}
