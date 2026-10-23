using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameTest.Runner;

/// <summary>The outcome of loading a folder: the valid tests, and one message per bad file.</summary>
public sealed record LoadResult(IReadOnlyList<TestDefinition> Tests, IReadOnlyList<string> Errors);

/// <summary>Reads test definitions from JSON files and rejects anything malformed.</summary>
public static class TestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // The files use snake_case ("coins_remaining"); C# uses PascalCase.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // A misspelled field ("timout_ms") is an error instead of being silently ignored.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly HashSet<string> ValidActions = new() { "press", "wait_for", "assert" };
    private static readonly HashSet<string> ValidKeys = new() { "Up", "Down", "Left", "Right", "Enter", "Escape" };
    private static readonly HashSet<string> ValidScenes = new() { "main_menu", "playing", "paused" };
    private static readonly Regex ValidName = new("^[A-Za-z0-9_]+$");

    /// <summary>Loads every *.json file in a folder, in a fixed (alphabetical) order.</summary>
    public static LoadResult LoadDirectory(string directory)
    {
        var tests = new List<TestDefinition>();
        var errors = new List<string>();

        if (!Directory.Exists(directory))
        {
            errors.Add($"test folder not found: {directory}");
            return new LoadResult(tests, errors);
        }

        string[] files = Directory.GetFiles(directory, "*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        if (files.Length == 0) errors.Add($"no .json test files in {directory}");

        var namesSeen = new Dictionary<string, string>();  // test name -> file that defined it
        foreach (string file in files)
        {
            string fileName = Path.GetFileName(file);
            try
            {
                TestDefinition test = LoadFile(file);
                if (namesSeen.TryGetValue(test.Name, out string? firstFile))
                {
                    errors.Add($"{fileName}: the name '{test.Name}' is already used by {firstFile}");
                    continue;
                }
                namesSeen[test.Name] = fileName;
                tests.Add(test);
            }
            catch (TestDefinitionException ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
            }
        }
        return new LoadResult(tests, errors);
    }

    /// <summary>Loads and validates one test file. Throws TestDefinitionException if it is bad.</summary>
    public static TestDefinition LoadFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TestDefinitionException($"cannot read the file: {ex.Message}");
        }

        TestDefinition? test;
        try
        {
            test = JsonSerializer.Deserialize<TestDefinition>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new TestDefinitionException($"invalid JSON: {ex.Message}");
        }
        if (test is null) throw new TestDefinitionException("the file is empty");

        test.SourceFile = path;
        Validate(test);
        return test;
    }

    private static void Validate(TestDefinition test)
    {
        if (string.IsNullOrEmpty(test.Name) || !ValidName.IsMatch(test.Name))
        {
            throw new TestDefinitionException(
                "'name' is required and may only contain letters, digits and underscores (it is used in file names)");
        }
        if (test.TimeoutMs <= 0)
        {
            throw new TestDefinitionException("'timeout_ms' must be greater than 0");
        }
        if (test.Steps is null || test.Steps.Count == 0)
        {
            throw new TestDefinitionException("'steps' must contain at least one step");
        }

        for (int i = 0; i < test.Steps.Count; i++)
        {
            TestStep? step = test.Steps[i];
            string label = $"step {i + 1}";
            if (step is null) throw new TestDefinitionException($"{label}: the step is empty (null)");
            ValidateStep(step, label);
        }
    }

    private static void ValidateStep(TestStep step, string label)
    {
        switch (step.Action)
        {
            case "press":
                Forbid(step.Expect is not null, "expect", step.Action, label);
                Forbid(step.TimeoutMs is not null, "timeout_ms", step.Action, label);
                ValidatePressKeys(step, label);
                if (step.DelayMs < 0) throw new TestDefinitionException($"{label}: 'delay_ms' cannot be negative");
                break;

            case "assert":
            case "wait_for":
                Forbid(step.Key is not null || step.Keys is not null, "key/keys", step.Action, label);
                Forbid(step.DelayMs != 0, "delay_ms", step.Action, label);
                if (step.Action == "assert") Forbid(step.TimeoutMs is not null, "timeout_ms", step.Action, label);
                if (step.TimeoutMs is <= 0) throw new TestDefinitionException($"{label}: 'timeout_ms' must be greater than 0");
                ValidateExpectation(step.Expect, label);
                break;

            default:
                throw new TestDefinitionException(
                    $"{label}: unknown action '{step.Action}' (use press, wait_for or assert)");
        }
    }

    private static void ValidatePressKeys(TestStep step, string label)
    {
        if ((step.Key is null) == (step.Keys is null))
        {
            throw new TestDefinitionException($"{label}: a 'press' step needs exactly one of 'key' or 'keys'");
        }

        List<string> keys = step.Key is not null ? new List<string> { step.Key } : step.Keys!;
        if (keys.Count == 0) throw new TestDefinitionException($"{label}: 'keys' must not be empty");

        foreach (string key in keys)
        {
            if (!ValidKeys.Contains(key))
            {
                throw new TestDefinitionException(
                    $"{label}: unknown key '{key}' (use Up, Down, Left, Right, Enter or Escape)");
            }
        }
    }

    private static void ValidateExpectation(StateExpectation? expect, string label)
    {
        if (expect is null || expect.IsEmpty)
        {
            throw new TestDefinitionException(
                $"{label}: 'expect' must check at least one of scene, player, score, coins_remaining");
        }
        if (expect.Scene is not null && !ValidScenes.Contains(expect.Scene))
        {
            throw new TestDefinitionException(
                $"{label}: unknown scene '{expect.Scene}' (use main_menu, playing or paused)");
        }
        if (expect.Player is not null && expect.Player.X is null && expect.Player.Y is null)
        {
            throw new TestDefinitionException($"{label}: 'player' must contain x, y or both");
        }
    }

    // Rejects a field that makes no sense for this kind of step, so it is never silently ignored.
    private static void Forbid(bool present, string field, string action, string label)
    {
        if (present) throw new TestDefinitionException($"{label}: '{field}' cannot be used in a '{action}' step");
    }
}

