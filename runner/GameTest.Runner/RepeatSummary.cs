using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameTest.Runner;

/// <summary>How one test behaved across all repeats of the suite.</summary>
public sealed record TestStability(
    string Name,
    int Runs,
    int Passed,
    int Failed,
    int TimedOut,
    bool Flaky,
    IReadOnlyList<string> Outcomes);

/// <summary>The result of running a suite several times.</summary>
public sealed record RepeatSummary(
    int RequestedRepeats,
    int CompletedRepeats,
    bool Interrupted,
    string TestFolder,
    IReadOnlyList<TestStability> Tests)
{
    public int FlakyCount => Tests.Count(t => t.Flaky);
    public bool AllPassedEveryTime => Tests.Count > 0 && Tests.All(t => t.Passed == t.Runs);
}

public static class StabilityCalculator
{
    /// <summary>Works out, per test, how often it passed, and whether its result changed between repeats.</summary>
    public static List<TestStability> Calculate(IReadOnlyList<IReadOnlyList<TestResult>> repeats)
    {
        var order = new List<string>();
        var outcomes = new Dictionary<string, List<TestOutcome>>();

        foreach (IReadOnlyList<TestResult> results in repeats)
        {
            foreach (TestResult result in results)
            {
                if (!outcomes.TryGetValue(result.Name, out List<TestOutcome>? list))
                {
                    list = new List<TestOutcome>();
                    outcomes[result.Name] = list;
                    order.Add(result.Name);
                }
                list.Add(result.Outcome);
            }
        }

        return order.Select(name =>
        {
            List<TestOutcome> list = outcomes[name];
            return new TestStability(
                name,
                list.Count,
                list.Count(o => o == TestOutcome.Pass),
                list.Count(o => o == TestOutcome.Fail),
                list.Count(o => o == TestOutcome.Timeout),
                list.Any(o => o == TestOutcome.Pass) && list.Any(o => o != TestOutcome.Pass),
                list.Select(o => o.ToLabel()).ToList());
        }).ToList();
    }
}

public static class RepeatSummaryWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(string folder, RepeatSummary summary)
    {
        Directory.CreateDirectory(folder);
        string path = Path.GetFullPath(Path.Combine(folder, "repeat_summary.json"));
        File.WriteAllText(path, JsonSerializer.Serialize(summary, Options), new UTF8Encoding(false));
        return path;
    }

    public static void Print(RepeatSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("==== Stability over repeats ====");
        Console.WriteLine($"{summary.CompletedRepeats} of {summary.RequestedRepeats} repeats completed" +
                          (summary.Interrupted ? " (interrupted)" : ""));
        Console.WriteLine();

        int width = Math.Max(4, summary.Tests.Max(t => t.Name.Length));
        Console.WriteLine($"{"Test".PadRight(width)}  Passed  Result");
        foreach (TestStability t in summary.Tests)
        {
            string verdict = t.Flaky ? "FLAKY" : t.Passed == t.Runs ? "stable" : "ALWAYS FAILS";
            string passed = $"{t.Passed}/{t.Runs}";
            Console.WriteLine($"{t.Name.PadRight(width)}  {passed,-6}  {verdict}");
        }

        Console.WriteLine();
        if (summary.FlakyCount > 0)
        {
            Console.WriteLine($"{summary.FlakyCount} flaky test(s): the result changed between repeats.");
            foreach (TestStability t in summary.Tests.Where(t => t.Flaky))
            {
                Console.WriteLine($"  {t.Name}: {string.Join(" ", t.Outcomes)}");
            }
        }
        else
        {
            Console.WriteLine("No test changed its result between repeats.");
        }
    }
}

