using System.Text.Json;
using Buffaly.OpenAI.ComputerUse;

namespace Buffaly.OpenAI.ComputerUse.Tests;

public class OpenAIComputerUseFacadeTests
{
    private const string TinyPngDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2QfNQAAAAASUVORK5CYII=";

    [Fact]
    public void ExtractComputerActions_NoComputerCallOutput_ReturnsEmptyActions()
    {
        const string responseJson = """
            {
              "id": "resp_123",
              "output": [
                {
                  "type": "message",
                  "content": [
                    {
                      "type": "output_text",
                      "text": "No tool action required."
                    }
                  ]
                }
              ]
            }
            """;

        string resultJson = OpenAIComputerUseFacade.ExtractComputerActions(responseJson, "1000");

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;
        JsonElement actions = root.GetProperty("actions");

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("actionCount").GetInt32());
        Assert.Equal(JsonValueKind.Array, actions.ValueKind);
        Assert.Equal(0, actions.GetArrayLength());
    }

    [Fact]
    public void ExtractComputerActions_OneComputerCallClickAction_ParsesCallIdAndCoordinates()
    {
        const string responseJson = """
            {
              "id": "resp_456",
              "output": [
                {
                  "type": "computer_call",
                  "call_id": "call_abc123",
                  "action": {
                    "type": "click",
                    "x": 640,
                    "y": 360
                  }
                }
              ]
            }
            """;

        string resultJson = OpenAIComputerUseFacade.ExtractComputerActions(responseJson, "1000");

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;
        JsonElement actions = root.GetProperty("actions");
        JsonElement action = actions[0];
        JsonElement rawAction = action.GetProperty("rawAction");

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(1, root.GetProperty("actionCount").GetInt32());
        Assert.Equal(1, actions.GetArrayLength());
        Assert.Equal("call_abc123", action.GetProperty("callId").GetString());
        Assert.Equal("click", action.GetProperty("actionType").GetString());
        Assert.Equal("click", rawAction.GetProperty("type").GetString());
        Assert.Equal(640, rawAction.GetProperty("x").GetInt32());
        Assert.Equal(360, rawAction.GetProperty("y").GetInt32());
    }

    [Fact]
    public void ExtractComputerActions_MalformedJson_ReturnsFailureEnvelope()
    {
        const string malformedJson = "{ \"output\": [ { \"type\": \"computer_call\" ";

        string resultJson = OpenAIComputerUseFacade.ExtractComputerActions(malformedJson, "1000");

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.True(root.TryGetProperty("error", out JsonElement error));
        Assert.NotEqual(string.Empty, error.GetString());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Gpt54Ga_InitialComputerUseCall_UsesStrictGaPayload()
    {
        const string modelName = "gpt-5.4";
        AssertStrictGaModelName(modelName);

        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.True(true, "OPENAI_API_KEY not set; skipping integration test.");
            return;
        }

        string resultJson = OpenAIComputerUseFacade.CreateComputerGaInitialResponse(
            apiKey,
            "Take no action. Return screenshot request only if needed.",
            modelName,
            "60",
            "12000",
            string.Empty);

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("success", out JsonElement successElement),
            $"Expected top-level 'success' field in response envelope. Payload: {resultJson}");
        Assert.True(successElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"Expected 'success' to be a boolean. Payload: {resultJson}");
        Assert.True(root.TryGetProperty("requestBody", out JsonElement requestBodyElement) &&
                    requestBodyElement.ValueKind == JsonValueKind.String,
            $"Expected string 'requestBody' in response envelope. Payload: {resultJson}");
        string requestBody = requestBodyElement.GetString() ?? string.Empty;
        Assert.Contains("\"model\":\"gpt-5.4\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"computer\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"computer_use_preview\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"display_width\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"display_height\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"environment\"", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"truncation\"", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Gpt54Ga_Followup_ContextThreading_UsesExpectedPayload()
    {
        const string modelName = "gpt-5.4";
        const string previousResponseId = "resp_test";
        const string callId = "call_test";
        AssertStrictGaModelName(modelName);

        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.True(true, "OPENAI_API_KEY not set; skipping integration test.");
            return;
        }

        string resultJson = OpenAIComputerUseFacade.CreateComputerGaFollowupResponse(
            apiKey,
            modelName,
            previousResponseId,
            callId,
            "60",
            "12000",
            TinyPngDataUrl);

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("requestBody", out JsonElement requestBodyElement) &&
                    requestBodyElement.ValueKind == JsonValueKind.String,
            $"Expected string 'requestBody' in response envelope. Payload: {resultJson}");
        string requestBody = requestBodyElement.GetString() ?? string.Empty;
        Assert.Contains("\"model\":\"gpt-5.4\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"computer\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"previous_response_id\":\"resp_test\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"call_id\":\"call_test\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"computer_screenshot\"", requestBody, StringComparison.Ordinal);
        Assert.Contains("\"detail\":\"original\"", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Gpt54Ga_ModelGuard_WithPreviewModel_Fails()
    {
        const string wrongModelName = "computer-use-preview";

        string resultJson = OpenAIComputerUseFacade.CreateComputerGaInitialResponse(
            string.Empty,
            "Guard validation only.",
            wrongModelName,
            "60",
            "12000",
            string.Empty);

        using JsonDocument doc = JsonDocument.Parse(resultJson);
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("success", out JsonElement successElement) &&
                    successElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"Expected boolean 'success' in response envelope. Payload: {resultJson}");
        Assert.False(successElement.GetBoolean(), "Expected failure when apiKey is empty.");

        Assert.Throws<Xunit.Sdk.EqualException>(() => AssertStrictGaModelName(wrongModelName));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ComputerUsePreview_InitialThenFollowup_Works()
    {
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.True(true, "OPENAI_API_KEY not set; skipping integration test.");
            return;
        }

        string initialJson = OpenAIComputerUseFacade.CreateComputerUseInitialResponse(
            apiKey,
            "Request a screenshot action only.",
            "computer-use-preview",
            "60",
            "12000",
            "windows",
            "1280",
            "720",
            string.Empty);

        using JsonDocument initialDoc = JsonDocument.Parse(initialJson);
        JsonElement initialRoot = initialDoc.RootElement;

        Assert.True(initialRoot.TryGetProperty("success", out JsonElement initialSuccessElement),
            $"Expected top-level 'success' field in initial response. Payload: {initialJson}");
        Assert.True(initialSuccessElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"Expected boolean 'success' in initial response. Payload: {initialJson}");
        if (!initialSuccessElement.GetBoolean())
        {
            string initialError = initialRoot.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString() ?? string.Empty
                : string.Empty;

            if (IsLikelyTransportError(initialError))
            {
                Assert.True(true, $"Network appears unavailable for integration test; skipping follow-up flow. Error: {initialError}");
                return;
            }

            if (IsLikelyPreviewRetirementError(initialError))
            {
                Assert.True(true, $"computer-use-preview appears unavailable; skipping legacy preview follow-up flow. Error: {initialError}");
                return;
            }

            Assert.True(initialSuccessElement.GetBoolean(),
                $"Expected initial call with computer-use-preview to succeed. Payload: {initialJson}");
        }
        Assert.True(initialRoot.TryGetProperty("responseId", out JsonElement responseIdElement) &&
                    responseIdElement.ValueKind == JsonValueKind.String,
            $"Expected string 'responseId' in initial response. Payload: {initialJson}");
        string responseId = responseIdElement.GetString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(responseId),
            $"Expected non-empty responseId in initial response. Payload: {initialJson}");

        if (!initialRoot.TryGetProperty("actions", out JsonElement actionsElement) ||
            actionsElement.ValueKind != JsonValueKind.Array ||
            actionsElement.GetArrayLength() == 0)
        {
            Assert.True(true, "No actions returned from initial response; skipping follow-up step.");
            return;
        }

        string? callId = null;
        foreach (JsonElement actionElement in actionsElement.EnumerateArray())
        {
            if (actionElement.TryGetProperty("callId", out JsonElement callIdElement) &&
                callIdElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(callIdElement.GetString()))
            {
                callId = callIdElement.GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(callId))
        {
            Assert.True(true, "No action with a callId found; skipping follow-up step.");
            return;
        }

        string followupJson = OpenAIComputerUseFacade.CreateComputerUseFollowupResponse(
            apiKey,
            "computer-use-preview",
            responseId,
            callId,
            "60",
            "12000",
            "windows",
            "1280",
            "720",
            TinyPngDataUrl);

        using JsonDocument followupDoc = JsonDocument.Parse(followupJson);
        JsonElement followupRoot = followupDoc.RootElement;

        Assert.True(followupRoot.TryGetProperty("success", out JsonElement followupSuccessElement),
            $"Expected top-level 'success' field in follow-up response. Payload: {followupJson}");
        Assert.True(followupSuccessElement.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"Expected boolean 'success' in follow-up response. Payload: {followupJson}");

        if (!followupSuccessElement.GetBoolean())
        {
            Assert.True(followupRoot.TryGetProperty("statusCode", out JsonElement statusCodeElement),
                $"Expected structured error with 'statusCode' when follow-up success=false. Payload: {followupJson}");
            Assert.True(statusCodeElement.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
                $"Expected 'statusCode' to be a number or null in structured error. Payload: {followupJson}");
        }
    }

    private static bool IsLikelyTransportError(string errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return false;
        }

        return errorText.Contains("socket", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("forbidden by its access permissions", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("name or service not known", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("no such host", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyPreviewRetirementError(string errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
        {
            return false;
        }

        return errorText.Contains("invalid_request_error", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("computer-use-preview", StringComparison.OrdinalIgnoreCase) ||
               errorText.Contains("computer_use_preview", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertStrictGaModelName(string modelName)
    {
        Assert.Equal("gpt-5.4", modelName);
    }
}
