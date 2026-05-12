using System.Text.Json;
using Buffaly.OpenAI.ComputerUse;

internal static class Program
{
    private static int Main(string[] args)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Missing required environment variable: OPENAI_API_KEY");
            return 1;
        }

        var instruction = args.Length > 0 ? string.Join(" ", args) : "Inspect the Windows desktop and request a screenshot if needed.";
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5.4";
        var timeoutSeconds = Environment.GetEnvironmentVariable("OPENAI_TIMEOUT_SECONDS") ?? "180";
        var maxOutputChars = Environment.GetEnvironmentVariable("OPENAI_MAX_OUTPUT_CHARS") ?? "20000";
        var screenshotDataUrl = Environment.GetEnvironmentVariable("OPENAI_SCREENSHOT_DATA_URL") ?? string.Empty;

        string initialJson = OpenAIComputerUseFacade.CreateComputerGaInitialResponse(
            apiKey,
            instruction,
            model,
            timeoutSeconds,
            maxOutputChars,
            screenshotDataUrl);

        string actionsJson = BuildActionExtraction(initialJson, maxOutputChars);

        string outputDir = Path.Combine(AppContext.BaseDirectory, "smoke-output");
        Directory.CreateDirectory(outputDir);

        string initialPath = Path.Combine(outputDir, "initial-response.json");
        string actionsPath = Path.Combine(outputDir, "parsed-actions.json");
        File.WriteAllText(initialPath, initialJson);
        File.WriteAllText(actionsPath, actionsJson);

        Console.WriteLine("Initial response:");
        Console.WriteLine(initialJson);
        Console.WriteLine();
        Console.WriteLine("Parsed actions:");
        Console.WriteLine(actionsJson);

        try
        {
            using var doc = JsonDocument.Parse(actionsJson);
            var root = doc.RootElement;
            bool success = root.TryGetProperty("success", out var successEl) && successEl.ValueKind == JsonValueKind.True;
            int actionCount = root.TryGetProperty("actionCount", out var countEl) && countEl.TryGetInt32(out var c) ? c : -1;

            Console.WriteLine($"status: success={success}, actionCount={actionCount}, outputDir={outputDir}");
            return success ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse action extraction output: {ex.Message}");
            return 3;
        }
    }

    private static string BuildActionExtraction(string initialJson, string maxOutputChars)
    {
        try
        {
            using var doc = JsonDocument.Parse(initialJson);
            if (doc.RootElement.TryGetProperty("responseJson", out var responseJsonEl)
                && responseJsonEl.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(responseJsonEl.GetString()))
            {
                return OpenAIComputerUseFacade.ExtractComputerActions(responseJsonEl.GetString()!, maxOutputChars);
            }

            return JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = (int?)null,
                requestBody = "",
                error = "initial response did not contain responseJson payload"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = (int?)null,
                requestBody = "",
                error = ex.Message
            });
        }
    }
}
