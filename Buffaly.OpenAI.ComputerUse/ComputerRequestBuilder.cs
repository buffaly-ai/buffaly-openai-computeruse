namespace Buffaly.OpenAI.ComputerUse;

public static class ComputerRequestBuilder
{
    private const string DesktopContext =
        "You are controlling a Windows desktop through a visual computer-use driver. Use desktop coordinates from the screenshots.";

    public static object BuildInitialRequest(string model, string instruction, string screenshotDataUrl, string? driverContext = null)
    {
        var content = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "input_text",
                ["text"] = BuildInstruction(instruction, driverContext)
            }
        };

        if (!string.IsNullOrWhiteSpace(screenshotDataUrl))
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "input_image",
                ["image_url"] = screenshotDataUrl.Trim(),
                ["detail"] = "original"
            });
        }

        return new Dictionary<string, object?>
        {
            ["model"] = ResolveModel(model),
            ["tools"] = BuildComputerTools(),
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = content.ToArray()
                }
            }
        };
    }

    public static object BuildFollowUpRequest(
        string model,
        string previousResponseId,
        string callId,
        string screenshotDataUrl)
    {
        return new Dictionary<string, object?>
        {
            ["model"] = ResolveModel(model),
            ["tools"] = BuildComputerTools(),
            ["previous_response_id"] = previousResponseId.Trim(),
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "computer_call_output",
                    ["call_id"] = callId.Trim(),
                    ["output"] = new Dictionary<string, object?>
                    {
                        ["type"] = "computer_screenshot",
                        ["image_url"] = screenshotDataUrl.Trim(),
                        ["detail"] = "original"
                    }
                }
            }
        };
    }

    private static object[] BuildComputerTools()
    {
        return new object[]
        {
            new Dictionary<string, object?>
            {
                ["type"] = "computer"
            }
        };
    }

    private static string ResolveModel(string model)
    {
        return string.IsNullOrWhiteSpace(model) ? ComputerUseDefaults.Model : model.Trim();
    }

    private static string BuildInstruction(string instruction, string? driverContext)
    {
        string userInstruction = string.IsNullOrWhiteSpace(instruction) ? string.Empty : instruction.Trim();
        string context = string.IsNullOrWhiteSpace(driverContext)
            ? DesktopContext
            : DesktopContext + Environment.NewLine + driverContext.Trim();
        return context + Environment.NewLine + Environment.NewLine + userInstruction;
    }
}
