using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public sealed class RunLogger
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    public RunLogger(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            IsEnabled = false;
            DirectoryPath = string.Empty;
            return;
        }

        IsEnabled = true;
        DirectoryPath = Path.Combine(
            rootDirectory.Trim(),
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public bool IsEnabled { get; }
    public string DirectoryPath { get; }

    public void WriteJson(string name, object value)
    {
        if (!IsEnabled)
        {
            return;
        }

        string json = value is string text
            ? PrettyJsonOrRaw(text)
            : JsonSerializer.Serialize(value, s_jsonOptions);
        File.WriteAllText(Path.Combine(DirectoryPath, name), json);
    }

    public void WriteScreenshotDataUrl(string name, string dataUrl)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(dataUrl))
        {
            return;
        }

        const string marker = "base64,";
        int markerIndex = dataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return;
        }

        string base64 = dataUrl[(markerIndex + marker.Length)..];
        byte[] bytes = Convert.FromBase64String(base64);
        File.WriteAllBytes(Path.Combine(DirectoryPath, name), bytes);
    }

    private static string PrettyJsonOrRaw(string text)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(doc.RootElement, s_jsonOptions);
        }
        catch
        {
            return text;
        }
    }
}
