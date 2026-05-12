using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public sealed class RunStateStore
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true
    };

    public RunStateStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("State file path is required.", nameof(path));
        }

        Path = path;
    }

    public string Path { get; }

    public bool TryLoad(out LoopState? state, out string error)
    {
        state = null;
        error = string.Empty;
        if (!File.Exists(Path))
        {
            return true;
        }

        try
        {
            string json = File.ReadAllText(Path);
            state = JsonSerializer.Deserialize<LoopState>(json, s_options);
            if (state == null)
            {
                error = "State file did not contain a valid state object.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Save(LoopState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.UpdatedUtc = DateTimeOffset.UtcNow;
        string? directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = Path + ".tmp";
        string json = JsonSerializer.Serialize(state, s_options);
        File.WriteAllText(tempPath, json);
        if (File.Exists(Path))
        {
            File.Replace(tempPath, Path, null);
        }
        else
        {
            File.Move(tempPath, Path);
        }
    }

    public void Delete()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}
