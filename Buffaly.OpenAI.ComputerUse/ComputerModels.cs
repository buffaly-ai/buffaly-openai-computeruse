using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public sealed class ComputerAction
{
    public string ActionType { get; init; } = string.Empty;
    public JsonElement RawAction { get; init; }
}

public sealed class ComputerCall
{
    public string CallId { get; init; } = string.Empty;
    public List<ComputerAction> Actions { get; init; } = new();
}

public sealed class ActionExecutionResult
{
    public string ActionType { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class DisplayInfo
{
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int CaptureWidth { get; init; }
    public int CaptureHeight { get; init; }
    public int VirtualDesktopLeft { get; init; }
    public int VirtualDesktopTop { get; init; }
    public int VirtualDesktopWidth { get; init; }
    public int VirtualDesktopHeight { get; init; }
    public string Scope { get; init; } = "desktop";
    public string WindowTitle { get; init; } = string.Empty;

    public bool IsValid => Width > 0 && Height > 0;
    public int EffectiveCaptureWidth => CaptureWidth > 0 ? CaptureWidth : Width;
    public int EffectiveCaptureHeight => CaptureHeight > 0 ? CaptureHeight : Height;
    public int EffectiveVirtualDesktopLeft => VirtualDesktopWidth > 0 ? VirtualDesktopLeft : Left;
    public int EffectiveVirtualDesktopTop => VirtualDesktopHeight > 0 ? VirtualDesktopTop : Top;
    public int EffectiveVirtualDesktopWidth => VirtualDesktopWidth > 0 ? VirtualDesktopWidth : EffectiveCaptureWidth;
    public int EffectiveVirtualDesktopHeight => VirtualDesktopHeight > 0 ? VirtualDesktopHeight : EffectiveCaptureHeight;
}

public sealed class CoordinateMapResult
{
    public bool Success { get; init; }
    public int ScreenshotX { get; init; }
    public int ScreenshotY { get; init; }
    public int AbsoluteX { get; init; }
    public int AbsoluteY { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class TargetWindowInfo
{
    public long Handle { get; set; }
    public uint ProcessId { get; set; }
    public string InitialTitle { get; set; } = string.Empty;
    public string LastTitle { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public DateTimeOffset ResolvedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LoopState
{
    public string Model { get; set; } = ComputerUseDefaults.Model;
    public string Endpoint { get; set; } = ComputerUseDefaults.Endpoint;
    public string Instruction { get; set; } = string.Empty;
    public string? PreviousResponseId { get; set; }
    public string? PendingCallId { get; set; }
    public int StepCount { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DisplayInfo? Display { get; set; }
    public TargetWindowInfo? TargetWindow { get; set; }
}

public static class ComputerUseDefaults
{
    public const string Model = "gpt-5.4";
    public const string Endpoint = "https://api.openai.com/v1/responses";
    public const int MaxSteps = 40;
    public const int MaxStepsPerRun = 4;
    public const string StateFileName = ".computeruse_state.json";
    public const string LogDirectoryName = "computeruse-runs";
}
