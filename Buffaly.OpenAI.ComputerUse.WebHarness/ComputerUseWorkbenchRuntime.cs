using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BasicUtilities;
using Buffaly.OpenAI.ComputerUse;

namespace Buffaly.OpenAI.ComputerUse.WebHarness;

public sealed class ComputerUseWorkbenchRuntime
{
    private readonly ConcurrentDictionary<string, ComputerUseRunState> m_runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly string m_apiKey;
    private readonly string m_outputRoot;
    private readonly string m_runnerProjectPath;

    private ComputerUseWorkbenchRuntime(IConfiguration configuration)
    {
        m_outputRoot = Path.GetFullPath(configuration["ComputerUseHarness:OutputRoot"]
            ?? Path.Combine("C:\\temp", "computer-use-web-harness"));
        Directory.CreateDirectory(m_outputRoot);

        m_apiKey = ResolveApiKey(configuration);
		m_runnerProjectPath = ResolveRunnerProjectPath(configuration);
		WriteDebugEvent("Runtime initialized", $"OutputRoot={m_outputRoot}; RunnerProjectPath={m_runnerProjectPath}; HasApiKey={!string.IsNullOrWhiteSpace(m_apiKey)}");
	}

    public static ComputerUseWorkbenchRuntime Create(IConfiguration configuration)
    {
        return new ComputerUseWorkbenchRuntime(configuration);
    }

    public ComputerUseWorkbenchConfig GetConfig()
    {
        return new ComputerUseWorkbenchConfig
        {
            HasApiKey = !string.IsNullOrWhiteSpace(m_apiKey),
            OutputRoot = m_outputRoot,
            RunnerProjectPath = m_runnerProjectPath,
            DefaultModel = ComputerUseDefaults.Model
        };
    }

    public ComputerUseRunSnapshot StartRun(StartComputerUseRunRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(m_apiKey))
        {
            throw new InvalidOperationException("OpenAI API key was not found.");
        }

        string direction = NormalizeRequired(request.Direction, "direction");
        if (!File.Exists(m_runnerProjectPath))
        {
            throw new FileNotFoundException("Computer use runner project was not found.", m_runnerProjectPath);
        }

        InterruptActiveRuns("Starting replacement run");

        string runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8];
        string runDirectory = Path.Combine(m_outputRoot, runId);
        Directory.CreateDirectory(runDirectory);
        string driverLogRoot = Path.Combine(runDirectory, "driver-logs");
        string dotnetCliHome = Path.Combine(runDirectory, "dotnet-cli-home");
        string dotnetUserProfile = Path.Combine(dotnetCliHome, "profile");
        string dotnetAppData = Path.Combine(dotnetUserProfile, "AppData", "Roaming");
        string dotnetLocalAppData = Path.Combine(dotnetUserProfile, "AppData", "Local");
        string dotnetNuGetPackages = Path.Combine(dotnetCliHome, "nuget-packages");
        string stateFile = Path.Combine(runDirectory, "state.json");
        string stdoutPath = Path.Combine(runDirectory, "stdout.log");
        string stderrPath = Path.Combine(runDirectory, "stderr.log");
        string requestPath = Path.Combine(runDirectory, "request.json");
        Directory.CreateDirectory(dotnetCliHome);
        Directory.CreateDirectory(dotnetAppData);
        Directory.CreateDirectory(dotnetLocalAppData);
        Directory.CreateDirectory(dotnetNuGetPackages);
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }));

        var state = new ComputerUseRunState
        {
            RunId = runId,
            Direction = direction,
            Model = NormalizeOption(request.Model, ComputerUseDefaults.Model),
            Status = "starting",
            RunDirectory = runDirectory,
            StdoutPath = stdoutPath,
            StderrPath = stderrPath,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        if (!m_runs.TryAdd(runId, state))
        {
            throw new InvalidOperationException("Could not register run.");
        }

        WriteDebugEvent("StartRun", $"RunId={runId}; Model={state.Model}; Direction={direction}; RunDirectory={runDirectory}");

		List<string> arguments = BuildRunnerArguments(request, state.Model, m_runnerProjectPath, stateFile, driverLogRoot, direction);
		WriteDebugEvent("StartRun.Process", $"RunId={runId}; Arguments={string.Join(" ", arguments.Select(Quote))}");
		var startInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(m_runnerProjectPath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
		foreach (string argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}
        startInfo.Environment["OPENAI_API_KEY"] = m_apiKey;
        startInfo.Environment["DOTNET_CLI_HOME"] = dotnetCliHome;
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["USERPROFILE"] = dotnetUserProfile;
        startInfo.Environment["APPDATA"] = dotnetAppData;
        startInfo.Environment["LOCALAPPDATA"] = dotnetLocalAppData;
        startInfo.Environment["NUGET_PACKAGES"] = dotnetNuGetPackages;

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, args) => AppendLine(state, stdoutPath, args.Data, isError: false);
        process.ErrorDataReceived += (_, args) => AppendLine(state, stderrPath, args.Data, isError: true);
        process.Exited += (_, _) =>
        {
            state.ExitCode = process.ExitCode;
            state.Status = state.InterruptRequested
                ? "interrupted"
                : process.ExitCode == 0 ? "completed" : "failed";
            state.UpdatedUtc = DateTimeOffset.UtcNow;
            WriteDebugEvent("Run exited", $"RunId={state.RunId}; Status={state.Status}; ExitCode={state.ExitCode}");
            SafeDispose(process);
        };

        state.Process = process;
        if (!process.Start())
        {
            WriteDebugEvent("Process start failed", $"RunId={runId}; dotnet did not start a process.");
            throw new InvalidOperationException("Computer use runner process did not start.");
        }

        WriteDebugEvent("Process started", $"RunId={runId}; ProcessId={process.Id}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        state.Status = "running";
        state.UpdatedUtc = DateTimeOffset.UtcNow;

        return ToSnapshot(state);
    }

    public IReadOnlyList<ComputerUseRunSnapshot> ListRuns()
    {
        return m_runs.Values
            .OrderByDescending(run => run.CreatedUtc)
            .Select(ToSnapshot)
            .ToArray();
    }

    public ComputerUseRunSnapshot? GetRun(string runId)
    {
        return m_runs.TryGetValue(runId, out ComputerUseRunState? state) ? ToSnapshot(state) : null;
    }

    public bool InterruptRun(string runId)
    {
        if (!m_runs.TryGetValue(runId, out ComputerUseRunState? state))
        {
            WriteDebugEvent("InterruptRun missing", $"RunId={runId}");
            return false;
        }

        WriteDebugEvent("InterruptRun", $"RunId={runId}; Status={state.Status}");
        state.InterruptRequested = true;
        state.Status = "interrupting";
        state.UpdatedUtc = DateTimeOffset.UtcNow;

        try
        {
            if (state.Process is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            AppendLine(state, state.StderrPath, "Interrupt failed: " + ex.Message, isError: true);
        }

        return true;
    }

    public string? ResolveRunFile(string runId, string relativePath)
    {
        if (!m_runs.TryGetValue(runId, out ComputerUseRunState? state))
        {
            return null;
        }

        string root = Path.GetFullPath(state.RunDirectory);
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath ?? string.Empty));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            return null;
        }

        return candidate;
    }

    public string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".json" => "application/json",
            ".log" or ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private ComputerUseRunSnapshot ToSnapshot(ComputerUseRunState state)
    {
        string stdout = ReadTail(state.StdoutPath, 12000);
        string stderr = ReadTail(state.StderrPath, 8000);
        ComputerUseRunFile[] files = ListRunFiles(state);
        return new ComputerUseRunSnapshot
        {
            RunId = state.RunId,
            Direction = state.Direction,
            Model = state.Model,
            Status = state.Status,
            ExitCode = state.ExitCode,
            InterruptRequested = state.InterruptRequested,
            CreatedUtc = state.CreatedUtc,
            UpdatedUtc = state.UpdatedUtc,
            RunDirectory = state.RunDirectory,
            Stdout = stdout,
            Stderr = stderr,
            Files = files
        };
    }

    private ComputerUseRunFile[] ListRunFiles(ComputerUseRunState state)
    {
        if (!Directory.Exists(state.RunDirectory))
        {
            return [];
        }

        string root = Path.GetFullPath(state.RunDirectory);
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var info = new FileInfo(path);
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                return new ComputerUseRunFile
                {
                    Name = info.Name,
                    RelativePath = relative,
                    Url = "/computer-use/runfiles/" + Uri.EscapeDataString(state.RunId) + "/" + relative.Split('/').Select(Uri.EscapeDataString).Aggregate((a, b) => a + "/" + b),
                    Length = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc
                };
            })
            .OrderByDescending(file => file.LastWriteUtc)
            .ToArray();
    }

    private static List<string> BuildRunnerArguments(
		StartComputerUseRunRequest request,
		string model,
		string runnerProjectPath,
		string stateFile,
		string logRoot,
		string direction)
	{
		var args = new List<string>
		{
			"run",
			"--project",
			runnerProjectPath,
			"--",
			"--model",
            model,
            "--state-file",
            stateFile,
            "--log-dir",
            logRoot,
            "--max-steps",
            Math.Max(1, request.MaxSteps <= 0 ? ComputerUseDefaults.MaxSteps : request.MaxSteps).ToString(),
            "--max-steps-per-run",
            Math.Max(1, request.MaxStepsPerRun <= 0 ? ComputerUseDefaults.MaxStepsPerRun : request.MaxStepsPerRun).ToString(),
            "--screenshot-scale",
            (request.ScreenshotScale <= 0 ? 1.0 : request.ScreenshotScale).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--capture-scope",
            NormalizeCaptureScope(request.CaptureScope),
            "--popup-mode",
            NormalizePopupMode(request.PopupMode),
            "--reset",
            direction
        };

        if (!string.IsNullOrWhiteSpace(request.WindowTitle))
        {
			args.InsertRange(args.Count - 2, ["--window-title", request.WindowTitle.Trim()]);
        }

        if (!string.IsNullOrWhiteSpace(request.ProcessName))
        {
			args.InsertRange(args.Count - 2, ["--process-name", request.ProcessName.Trim()]);
        }

        return args;
    }

    private static string ResolveApiKey(IConfiguration configuration)
    {
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey.Trim();
        }

        string? configured = configuration["ComputerUseHarness:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return ReadApiKeyFromSettings(configuration["ComputerUseHarness:AppSettingsPath"]);
    }

    private static string ReadApiKeyFromSettings(string? settingsPath)
    {
        string resolvedPath = string.IsNullOrWhiteSpace(settingsPath)
            ? "C:\\dev\\BuffalyNet6\\Buffaly.Test\\appsettings.json"
            : settingsPath;

        if (!File.Exists(resolvedPath))
        {
            return string.Empty;
        }

        using FileStream stream = File.OpenRead(resolvedPath);
        using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (!document.RootElement.TryGetProperty("AppSettings", out JsonElement appSettings))
        {
            return string.Empty;
        }

        string nonZdrToken = ReadString(appSettings, "OpenAI.NonZdrToken");
        return string.IsNullOrWhiteSpace(nonZdrToken)
            ? ReadString(appSettings, "OpenAI.Token")
            : nonZdrToken;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

	private static string ResolveRunnerProjectPath(IConfiguration configuration)
	{
		string configuredRunnerProjectPath = configuration["ComputerUseHarness:RunnerProjectPath"] ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(configuredRunnerProjectPath))
		{
			return Path.GetFullPath(configuredRunnerProjectPath);
		}

		foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
		{
            DirectoryInfo? directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Buffaly.OpenAI.ComputerUse.Runner", "Buffaly.OpenAI.ComputerUse.Runner.csproj");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "Buffaly.OpenAI.ComputerUse.Runner",
            "Buffaly.OpenAI.ComputerUse.Runner.csproj"));
    }

    private void InterruptActiveRuns(string reason)
    {
        foreach (ComputerUseRunState state in m_runs.Values)
        {
            if (!IsActiveStatus(state.Status))
            {
                continue;
            }

            WriteDebugEvent("Interrupt active run", $"RunId={state.RunId}; Reason={reason}; Status={state.Status}");
            InterruptRun(state.RunId);
        }
    }

    private static bool IsActiveStatus(string status)
    {
        return status is "starting" or "running" or "interrupting";
    }

    private static void WriteDebugEvent(string eventName, string message)
    {
        try
        {
            Logs.DebugLog.WriteEvent("ComputerUseWorkbench." + eventName, message);
        }
        catch
        {
            // Debug logging must never break the harness runtime.
        }
    }

    private static void AppendLine(ComputerUseRunState state, string path, string? line, bool isError)
    {
        if (line == null)
        {
            return;
        }

        File.AppendAllText(path, line + Environment.NewLine);
        state.UpdatedUtc = DateTimeOffset.UtcNow;
        if (isError && state.Status == "running")
        {
            state.LastError = line;
        }
    }

    private static string ReadTail(string path, int maxChars)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        string text = File.ReadAllText(path);
        return text.Length <= maxChars ? text : text[^maxChars..];
    }

    private static string NormalizeRequired(string? value, string name)
    {
        string normalized = NormalizeOption(value, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(name + " is required.");
        }

        return normalized;
    }

    private static string NormalizeOption(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeCaptureScope(string? value)
    {
        string normalized = NormalizeOption(value, "desktop").ToLowerInvariant();
        return normalized is "desktop" or "window" or "client" ? normalized : "desktop";
    }

    private static string NormalizePopupMode(string? value)
    {
        string normalized = NormalizeOption(value, "active-owned").ToLowerInvariant();
        return normalized is "none" or "active-owned" ? normalized : "active-owned";
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void SafeDispose(Process process)
    {
        try
        {
            process.Dispose();
        }
        catch
        {
            // Process cleanup should not change run status.
        }
    }

    private sealed class ComputerUseRunState
    {
        public string RunId { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ExitCode { get; set; }
        public bool InterruptRequested { get; set; }
        public string RunDirectory { get; init; } = string.Empty;
        public string StdoutPath { get; init; } = string.Empty;
        public string StderrPath { get; init; } = string.Empty;
        public string LastError { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; init; }
        public DateTimeOffset UpdatedUtc { get; set; }
        public Process? Process { get; set; }
    }
}

public sealed class ComputerUseWorkbenchConfig
{
    public bool HasApiKey { get; set; }
    public string OutputRoot { get; set; } = string.Empty;
    public string RunnerProjectPath { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
}

public sealed class StartComputerUseRunRequest
{
    public string Direction { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxSteps { get; set; } = ComputerUseDefaults.MaxSteps;
    public int MaxStepsPerRun { get; set; } = ComputerUseDefaults.MaxStepsPerRun;
    public double ScreenshotScale { get; set; } = 1.0;
    public string CaptureScope { get; set; } = "desktop";
    public string WindowTitle { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string PopupMode { get; set; } = "active-owned";
}

public sealed class ComputerUseRunSnapshot
{
    public string RunId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public bool InterruptRequested { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string RunDirectory { get; set; } = string.Empty;
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public ComputerUseRunFile[] Files { get; set; } = [];
}

public sealed class ComputerUseRunFile
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime LastWriteUtc { get; set; }
}
