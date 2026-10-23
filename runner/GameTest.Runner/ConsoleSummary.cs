namespace GameTest.Runner;

/// <summary>Prints the end-of-run summary.</summary>
public static class ConsoleSummary
{
    public static void Print(IReadOnlyList<TestResult> results, TimeSpan total)
    {
        ReportSummary s = ReportSummary.From(results);

        Console.WriteLine();
        Console.WriteLine("==== Summary ====");
        Console.WriteLine($"{s.Total} run: {s.Passed} passed, {s.Failed} failed, {s.TimedOut} timed out  ({total.TotalSeconds:0.0} s)");

        List<TestResult> notPassed = results.Where(r => r.Outcome != TestOutcome.Pass).ToList();
        if (notPassed.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine("Not passed:");
        foreach (TestResult r in notPassed)
        {
            Console.WriteLine($"  {r.Outcome.ToLabel(),-8}{r.Name}");
            // A reason can span several lines (for example the game's own output): indent them all.
            foreach (string line in (r.FailureReason ?? "(no reason recorded)").Split('\n'))
            {
                Console.WriteLine($"      {line.TrimEnd('\r')}");
            }
        }
    }
}
