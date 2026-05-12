using System.Text.Json;
using Buffaly.OpenAI.ComputerUse;

namespace Buffaly.OpenAI.ComputerUse.Tests;

public class WindowsDesktopComputerUseTests
{
    private const string TinyPngDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2QfNQAAAAASUVORK5CYII=";

    [Fact]
    public void BuildInitialRequest_UsesGaComputerTool()
    {
        object payload = ComputerRequestBuilder.BuildInitialRequest("gpt-5.4", "Move the cursor.", TinyPngDataUrl);
        string json = JsonSerializer.Serialize(payload);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Equal("gpt-5.4", root.GetProperty("model").GetString());
        JsonElement tool = root.GetProperty("tools")[0];
        Assert.Equal("computer", tool.GetProperty("type").GetString());
        Assert.DoesNotContain("computer_use_preview", json, StringComparison.Ordinal);
        Assert.DoesNotContain("display_width", json, StringComparison.Ordinal);
        Assert.Contains("Windows desktop", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"input_image\"", json, StringComparison.Ordinal);
        Assert.Contains("\"detail\":\"original\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildInitialRequest_IncludesScopedDriverContext()
    {
        object payload = ComputerRequestBuilder.BuildInitialRequest(
            "gpt-5.4",
            "Click OK.",
            TinyPngDataUrl,
            "The screenshot viewport is scoped to 'client'.");
        string json = JsonSerializer.Serialize(payload);

        Assert.Contains("scoped", json, StringComparison.Ordinal);
        Assert.Contains("client", json, StringComparison.Ordinal);
        Assert.Contains("Click OK.", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFollowUpRequest_UsesComputerScreenshotOutput()
    {
        object payload = ComputerRequestBuilder.BuildFollowUpRequest("gpt-5.4", "resp_123", "call_123", TinyPngDataUrl);
        string json = JsonSerializer.Serialize(payload);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Equal("gpt-5.4", root.GetProperty("model").GetString());
        Assert.Equal("resp_123", root.GetProperty("previous_response_id").GetString());
        Assert.Equal("computer", root.GetProperty("tools")[0].GetProperty("type").GetString());
        JsonElement output = root.GetProperty("input")[0];
        Assert.Equal("computer_call_output", output.GetProperty("type").GetString());
        Assert.Equal("call_123", output.GetProperty("call_id").GetString());
        Assert.Equal("computer_screenshot", output.GetProperty("output").GetProperty("type").GetString());
        Assert.Equal("original", output.GetProperty("output").GetProperty("detail").GetString());
    }

    [Fact]
    public void ParseComputerCalls_TopLevelActions_PreservesOrder()
    {
        const string responseJson = """
            {
              "id": "resp_123",
              "output": [
                {
                  "type": "computer_call",
                  "call_id": "call_abc",
                  "actions": [
                    { "type": "move", "x": 10, "y": 20 },
                    { "type": "click", "x": 10, "y": 20, "button": "left" }
                  ]
                }
              ]
            }
            """;

        List<ComputerCall> calls = ComputerResponseParser.ParseComputerCalls(responseJson);

        Assert.Single(calls);
        Assert.Equal("call_abc", calls[0].CallId);
        Assert.Equal(2, calls[0].Actions.Count);
        Assert.Equal("move", calls[0].Actions[0].ActionType);
        Assert.Equal("click", calls[0].Actions[1].ActionType);
        Assert.Equal(10, calls[0].Actions[1].RawAction.GetProperty("x").GetInt32());
    }

    [Fact]
    public void ExtractComputerActions_TopLevelActions_ReturnsFlatActions()
    {
        const string responseJson = """
            {
              "id": "resp_123",
              "output": [
                {
                  "type": "computer_call",
                  "call_id": "call_abc",
                  "actions": [
                    { "type": "move", "x": 10, "y": 20 },
                    { "type": "click", "x": 10, "y": 20 }
                  ]
                }
              ]
            }
            """;

        string resultJson = OpenAIComputerUseFacade.ExtractComputerActions(responseJson, "4000");
        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("actionCount").GetInt32());
        Assert.Equal("call_abc", root.GetProperty("actions")[0].GetProperty("callId").GetString());
        Assert.Equal("move", root.GetProperty("actions")[0].GetProperty("actionType").GetString());
        Assert.Equal("click", root.GetProperty("actions")[1].GetProperty("actionType").GetString());
    }

    [Fact]
    public void ParseComputerCalls_LegacyActionObject_StillParses()
    {
        const string responseJson = """
            {
              "id": "resp_123",
              "output": [
                {
                  "type": "computer_call",
                  "call_id": "call_legacy",
                  "action": { "type": "scroll", "scroll_y": -400 }
                }
              ]
            }
            """;

        List<ComputerCall> calls = ComputerResponseParser.ParseComputerCalls(responseJson);

        Assert.Single(calls);
        Assert.Equal("call_legacy", calls[0].CallId);
        Assert.Single(calls[0].Actions);
        Assert.Equal("scroll", calls[0].Actions[0].ActionType);
    }

    [Fact]
    public void CoordinateMapper_HandlesNegativeOriginDisplay()
    {
        using JsonDocument doc = JsonDocument.Parse("""{ "x": 25.4, "y": "10" }""");
        var display = new DisplayInfo
        {
            Left = -1920,
            Top = 0,
            Width = 3840,
            Height = 1080
        };

        CoordinateMapResult result = CoordinateMapper.Map(doc.RootElement, display);

        Assert.True(result.Success, result.Message);
        Assert.Equal(25, result.ScreenshotX);
        Assert.Equal(10, result.ScreenshotY);
        Assert.Equal(-1895, result.AbsoluteX);
        Assert.Equal(10, result.AbsoluteY);
    }

    [Fact]
    public void CoordinateMapper_RejectsOutOfBoundsCoordinates()
    {
        using JsonDocument doc = JsonDocument.Parse("""{ "x": 200, "y": 10 }""");
        var display = new DisplayInfo
        {
            Left = 0,
            Top = 0,
            Width = 100,
            Height = 100
        };

        CoordinateMapResult result = CoordinateMapper.Map(doc.RootElement, display);

        Assert.False(result.Success);
        Assert.Contains("outside", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoordinateMapper_ScalesScreenshotCoordinatesToCapturedViewport()
    {
        using JsonDocument doc = JsonDocument.Parse("""{ "x": 50, "y": 25 }""");
        var display = new DisplayInfo
        {
            Left = 100,
            Top = 200,
            Width = 101,
            Height = 51,
            CaptureWidth = 201,
            CaptureHeight = 101
        };

        CoordinateMapResult result = CoordinateMapper.Map(doc.RootElement, display);

        Assert.True(result.Success, result.Message);
        Assert.Equal(200, result.AbsoluteX);
        Assert.Equal(250, result.AbsoluteY);
    }

    [Fact]
    public void RunStateStore_CorruptJson_ReturnsError()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "state.json");
        File.WriteAllText(path, "{ not json");
        var store = new RunStateStore(path);

        bool loaded = store.TryLoad(out LoopState? state, out string error);

        Assert.False(loaded);
        Assert.Null(state);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RunStateStore_SaveAndLoad_RoundTrips()
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, "state.json");
        var store = new RunStateStore(path);
        var state = new LoopState
        {
            Model = "gpt-5.4",
            Endpoint = ComputerUseDefaults.Endpoint,
            Instruction = "test",
            PreviousResponseId = "resp_1",
            PendingCallId = "call_1",
            StepCount = 2,
            Display = new DisplayInfo { Left = -1, Top = 2, Width = 3, Height = 4 }
        };

        store.Save(state);
        bool loaded = store.TryLoad(out LoopState? loadedState, out string error);

        Assert.True(loaded, error);
        Assert.NotNull(loadedState);
        Assert.Equal("test", loadedState!.Instruction);
        Assert.Equal("call_1", loadedState.PendingCallId);
        Assert.Equal(-1, loadedState.Display!.Left);
    }

    [Fact]
    public void RunLogger_WritesJsonAndScreenshot()
    {
        string directory = CreateTempDirectory();
        var logger = new RunLogger(directory);

        logger.WriteJson("request.json", new { model = "gpt-5.4" });
        logger.WriteScreenshotDataUrl("screen.png", TinyPngDataUrl);

        Assert.True(File.Exists(Path.Combine(logger.DirectoryPath, "request.json")));
        Assert.True(File.Exists(Path.Combine(logger.DirectoryPath, "screen.png")));
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "computeruse-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
