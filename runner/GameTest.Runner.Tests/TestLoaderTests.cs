using Xunit;

namespace GameTest.Runner.Tests;

public class TestLoaderTests
{
    private const string MinimalValid = """
        { "name": "ok", "steps": [ { "action": "press", "key": "Right" } ] }
        """;

    private static string ErrorFor(string json)
    {
        using var temp = new TempFolder();
        string path = temp.Write("t.json", json);
        return Assert.Throws<TestDefinitionException>(() => TestLoader.LoadFile(path)).Message;
    }

    [Fact]
    public void A_valid_file_loads_with_snake_case_fields()
    {
        using var temp = new TempFolder();
        string path = temp.Write("t.json", """
            {
              "name": "ok",
              "timeout_ms": 1234,
              "steps": [
                { "action": "press", "key": "Right" },
                { "action": "assert", "expect": { "player": { "x": 2 }, "coins_remaining": 3 } }
              ]
            }
            """);

        TestDefinition test = TestLoader.LoadFile(path);

        Assert.Equal("ok", test.Name);
        Assert.Equal(1234, test.TimeoutMs);
        Assert.Equal(2, test.Steps.Count);
        Assert.Equal(2, test.Steps[1].Expect!.Player!.X);
        Assert.Null(test.Steps[1].Expect!.Player!.Y);
        Assert.Equal(3, test.Steps[1].Expect!.CoinsRemaining);
    }

    [Fact]
    public void An_unknown_key_name_is_rejected()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [ { "action": "press", "key": "Rigth" } ] }""");
        Assert.Contains("unknown key 'Rigth'", error);
    }

    [Fact]
    public void A_misspelled_field_is_rejected_not_ignored()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [ { "actoin": "press", "key": "Right" } ] }""");
        Assert.Contains("invalid JSON", error);
        Assert.Contains("actoin", error);
    }

    [Fact]
    public void An_unknown_action_is_rejected()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [ { "action": "jump" } ] }""");
        Assert.Contains("unknown action 'jump'", error);
    }

    [Fact]
    public void An_assert_step_without_expect_is_rejected()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [ { "action": "assert" } ] }""");
        Assert.Contains("'expect' must check at least one", error);
    }

    [Fact]
    public void A_press_step_needs_exactly_one_of_key_or_keys()
    {
        string neither = ErrorFor("""{ "name": "t", "steps": [ { "action": "press" } ] }""");
        string both = ErrorFor("""{ "name": "t", "steps": [ { "action": "press", "key": "Up", "keys": ["Down"] } ] }""");

        Assert.Contains("exactly one of 'key' or 'keys'", neither);
        Assert.Contains("exactly one of 'key' or 'keys'", both);
    }

    [Fact]
    public void A_screenshot_label_with_a_space_is_rejected()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [ { "action": "screenshot", "label": "has space" } ] }""");
        Assert.Contains("'label' may only contain letters, digits and underscores", error);
    }

    [Fact]
    public void A_test_name_with_spaces_is_rejected()
    {
        string error = ErrorFor("""{ "name": "my test", "steps": [ { "action": "press", "key": "Up" } ] }""");
        Assert.Contains("'name' is required", error);
    }

    [Fact]
    public void A_test_without_steps_is_rejected()
    {
        string error = ErrorFor("""{ "name": "t", "steps": [] }""");
        Assert.Contains("at least one step", error);
    }

    [Fact]
    public void One_bad_file_does_not_hide_the_good_ones_and_order_is_alphabetical()
    {
        using var temp = new TempFolder();
        temp.Write("c.json", MinimalValid.Replace("\"ok\"", "\"c_test\""));
        temp.Write("a.json", MinimalValid.Replace("\"ok\"", "\"a_test\""));
        temp.Write("b.json", """{ "name": "b_test", "steps": [ { "action": "press", "key": "Rigth" } ] }""");

        LoadResult loaded = TestLoader.LoadDirectory(temp.Folder);

        Assert.Equal(new[] { "a_test", "c_test" }, loaded.Tests.Select(t => t.Name).ToArray());
        Assert.Single(loaded.Errors);
        Assert.StartsWith("b.json:", loaded.Errors[0]);
    }

    [Fact]
    public void Two_files_with_the_same_test_name_are_reported()
    {
        using var temp = new TempFolder();
        temp.Write("a.json", MinimalValid);
        temp.Write("b.json", MinimalValid);

        LoadResult loaded = TestLoader.LoadDirectory(temp.Folder);

        Assert.Single(loaded.Tests);
        Assert.Contains("already used by a.json", Assert.Single(loaded.Errors));
    }

    [Fact]
    public void A_missing_folder_is_reported_not_thrown()
    {
        LoadResult loaded = TestLoader.LoadDirectory(Path.Combine(Path.GetTempPath(), "gametest_does_not_exist_" + Guid.NewGuid()));
        Assert.Empty(loaded.Tests);
        Assert.Contains("test folder not found", Assert.Single(loaded.Errors));
    }

    [Fact]
    public void An_empty_folder_is_reported()
    {
        using var temp = new TempFolder();
        LoadResult loaded = TestLoader.LoadDirectory(temp.Folder);
        Assert.Contains("no .json test files", Assert.Single(loaded.Errors));
    }
}
