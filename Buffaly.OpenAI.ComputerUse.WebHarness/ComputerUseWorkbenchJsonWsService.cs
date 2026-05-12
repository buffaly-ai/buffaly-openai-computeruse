using WebAppUtilities;

namespace Buffaly.OpenAI.ComputerUse.WebHarness;

public sealed class ComputerUseWorkbenchJsonWsService : JsonWs
{
    private static readonly Lazy<ComputerUseWorkbenchRuntime> s_runtime = new(BuildRuntime);

    public static ComputerUseWorkbenchRuntime GetRuntime() => s_runtime.Value;

    [JsonWsSerialize(SerializeResultsOptions.Full)]
    public static ComputerUseWorkbenchConfigContract GetConfig(ComputerUseWorkbenchConfigRequestContract request)
    {
        _ = request;
        return ToContract(GetRuntime().GetConfig());
    }

    [JsonWsSerialize(SerializeResultsOptions.Full)]
    public static ComputerUseRunSnapshotContract[] ListRuns(ComputerUseListRunsRequestContract request)
    {
        _ = request;
        return GetRuntime().ListRuns().Select(ToContract).ToArray();
    }

    [JsonWsSerialize(SerializeResultsOptions.Full)]
    public static ComputerUseRunSnapshotContract GetRun(ComputerUseGetRunRequestContract request)
    {
        if (request == null)
        {
            throw new JsonWsException("request is required.");
        }

        ComputerUseRunSnapshot? run = GetRuntime().GetRun(NormalizeRequired(request.RunId, "RunId"));
        if (run == null)
        {
            throw new JsonWsException("Run not found.");
        }

        return ToContract(run);
    }

    [JsonWsSerialize(SerializeResultsOptions.Full)]
    public static ComputerUseRunSnapshotContract StartRun(StartComputerUseRunRequest request)
    {
        if (request == null)
        {
            throw new JsonWsException("request is required.");
        }

        try
        {
            return ToContract(GetRuntime().StartRun(request));
        }
        catch (Exception ex)
        {
            throw new JsonWsException(ex.Message);
        }
    }

    [JsonWsSerialize(SerializeResultsOptions.Full)]
    public static ComputerUseInterruptRunResultContract InterruptRun(ComputerUseInterruptRunRequestContract request)
    {
        if (request == null)
        {
            throw new JsonWsException("request is required.");
        }

        bool success = GetRuntime().InterruptRun(NormalizeRequired(request.RunId, "RunId"));
        return new ComputerUseInterruptRunResultContract
        {
            Success = success,
            Error = success ? string.Empty : "Run not found."
        };
    }

    private static ComputerUseWorkbenchRuntime BuildRuntime()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return ComputerUseWorkbenchRuntime.Create(configuration);
    }

    private static ComputerUseWorkbenchConfigContract ToContract(ComputerUseWorkbenchConfig config)
    {
        return new ComputerUseWorkbenchConfigContract
        {
            HasApiKey = config.HasApiKey,
            OutputRoot = config.OutputRoot,
            RunnerProjectPath = config.RunnerProjectPath,
            DefaultModel = config.DefaultModel
        };
    }

    private static ComputerUseRunSnapshotContract ToContract(ComputerUseRunSnapshot run)
    {
        return new ComputerUseRunSnapshotContract
        {
            RunId = run.RunId,
            Direction = run.Direction,
            Model = run.Model,
            Status = run.Status,
            ExitCode = run.ExitCode,
            InterruptRequested = run.InterruptRequested,
            CreatedUtc = run.CreatedUtc,
            UpdatedUtc = run.UpdatedUtc,
            RunDirectory = run.RunDirectory,
            Stdout = run.Stdout,
            Stderr = run.Stderr,
            Files = run.Files.Select(file => new ComputerUseRunFileContract
            {
                Name = file.Name,
                RelativePath = file.RelativePath,
                Url = file.Url,
                Length = file.Length,
                LastWriteUtc = file.LastWriteUtc
            }).ToArray()
        };
    }

    private static string NormalizeRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonWsException(name + " is required.");
        }

        return value.Trim();
    }
}

public sealed class ComputerUseWorkbenchConfigRequestContract
{
    public string Reserved { get; set; } = string.Empty;
}

public sealed class ComputerUseListRunsRequestContract
{
    public string Reserved { get; set; } = string.Empty;
}

public sealed class ComputerUseGetRunRequestContract
{
    public string RunId { get; set; } = string.Empty;
}

public sealed class ComputerUseInterruptRunRequestContract
{
    public string RunId { get; set; } = string.Empty;
}

public sealed class ComputerUseInterruptRunResultContract
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class ComputerUseWorkbenchConfigContract
{
    public bool HasApiKey { get; set; }
    public string OutputRoot { get; set; } = string.Empty;
    public string RunnerProjectPath { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
}

public sealed class ComputerUseRunSnapshotContract
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
    public ComputerUseRunFileContract[] Files { get; set; } = [];
}

public sealed class ComputerUseRunFileContract
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime LastWriteUtc { get; set; }
}
