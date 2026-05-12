using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Buffaly.OpenAI.ComputerUse;

internal static class Program
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SwRestore = 9;

    private const int InputMouse = 0;
    private const int InputKeyboard = 1;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfRightdown = 0x0008;
    private const uint MouseeventfRightup = 0x0010;
    private const uint MouseeventfMiddledown = 0x0020;
    private const uint MouseeventfMiddleup = 0x0040;
    private const uint MouseeventfWheel = 0x0800;
    private const uint MouseeventfHwheel = 0x01000;
    private const uint MouseeventfVirtualdesk = 0x4000;
    private const uint MouseeventfAbsolute = 0x8000;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;

    private static readonly Dictionary<string, ushort> SpecialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11,
        ["control"] = 0x11,
        ["shift"] = 0x10,
        ["alt"] = 0x12,
        ["menu"] = 0x12,
        ["win"] = 0x5B,
        ["lwin"] = 0x5B,
        ["rwin"] = 0x5C,
        ["enter"] = 0x0D,
        ["return"] = 0x0D,
        ["tab"] = 0x09,
        ["esc"] = 0x1B,
        ["escape"] = 0x1B,
        ["space"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E,
        ["del"] = 0x2E,
        ["up"] = 0x26,
        ["down"] = 0x28,
        ["left"] = 0x25,
        ["right"] = 0x27,
        ["arrowup"] = 0x26,
        ["arrowdown"] = 0x28,
        ["arrowleft"] = 0x25,
        ["arrowright"] = 0x27,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["pgup"] = 0x21,
        ["pgdn"] = 0x22,
        ["insert"] = 0x2D,
        ["ins"] = 0x2D,
        ["capslock"] = 0x14,
    };

    public static int Main(string[] args)
    {
        try
        {
            RunnerOptions options = RunnerOptions.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(RunnerOptions.HelpText);
                return 0;
            }

            string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.Error.WriteLine("Missing OPENAI_API_KEY environment variable.");
                return 1;
            }

            var stateStore = new RunStateStore(options.StateFile);
            if (options.Reset)
            {
                stateStore.Delete();
                Console.WriteLine($"Cleared state file: {options.StateFile}");
            }

            if (!stateStore.TryLoad(out LoopState? loadedState, out string stateError))
            {
                Console.Error.WriteLine($"Ignoring corrupt state file '{options.StateFile}': {stateError}");
                stateStore.Delete();
                loadedState = null;
            }

            LoopState state = ResolveState(options, loadedState);
            if (string.IsNullOrWhiteSpace(state.Instruction))
            {
                Console.Error.WriteLine("Missing instruction. Pass it as command-line arguments or resume from valid state.");
                return 1;
            }

            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var logger = new RunLogger(options.LogDirectory);
            if (logger.IsEnabled)
            {
                Console.WriteLine($"Run log: {logger.DirectoryPath}");
            }

            return RunExecutionLoop(http, stateStore, logger, state, options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static LoopState ResolveState(RunnerOptions options, LoopState? loadedState)
    {
        string instruction = options.Instruction.Trim();
        bool hasNewInstruction = !string.IsNullOrWhiteSpace(instruction);
        if (loadedState != null
            && !options.Reset
            && (!hasNewInstruction || string.Equals(loadedState.Instruction, instruction, StringComparison.Ordinal))
            && string.Equals(loadedState.Model, options.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(loadedState.Endpoint, options.Endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return loadedState;
        }

        return new LoopState
        {
            Model = options.Model,
            Endpoint = options.Endpoint,
            Instruction = instruction,
            PreviousResponseId = null,
            PendingCallId = null,
            StepCount = 0,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    private static int RunExecutionLoop(
        HttpClient http,
        RunStateStore stateStore,
        RunLogger logger,
        LoopState state,
        RunnerOptions options)
    {
        if (state.StepCount >= options.MaxSteps)
        {
            Console.WriteLine($"Reached max steps ({options.MaxSteps}); stopping.");
            return 0;
        }

        stateStore.Save(state);
        int runSteps = 0;
        while (state.StepCount < options.MaxSteps && runSteps < options.MaxStepsPerRun)
        {
            int step = state.StepCount + 1;
            ScreenshotFrame screenshot = CaptureScreenshot(options, state);
            state.Display = screenshot.Display;
            string screenshotDataUrl = screenshot.DataUrl;
            logger.WriteScreenshotDataUrl($"screenshot-before-{step:000}.png", screenshotDataUrl);

            object payload = state.PreviousResponseId == null
                ? ComputerRequestBuilder.BuildInitialRequest(state.Model, state.Instruction, screenshotDataUrl, BuildDriverContext(screenshot.Display))
                : ComputerRequestBuilder.BuildFollowUpRequest(
                    state.Model,
                    state.PreviousResponseId,
                    RequirePendingCallId(state),
                    screenshotDataUrl);

            string requestJson = JsonSerializer.Serialize(payload);
            logger.WriteJson($"request-{step:000}.json", requestJson);

            using var request = new HttpRequestMessage(HttpMethod.Post, state.Endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = http.Send(request);
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            logger.WriteJson($"response-{step:000}.json", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"HTTP error {(int)response.StatusCode}: {response.ReasonPhrase}");
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    Console.Error.WriteLine(responseBody);
                }

                stateStore.Save(state);
                return 1;
            }

            using JsonDocument doc = JsonDocument.Parse(responseBody);
            JsonElement root = doc.RootElement;
            string? previousResponseId = ComputerResponseParser.ReadResponseId(root);
            if (string.IsNullOrWhiteSpace(previousResponseId))
            {
                Console.Error.WriteLine("Fatal parse error: response id missing.");
                stateStore.Save(state);
                return 1;
            }

            List<ComputerCall> calls = ComputerResponseParser.ParseComputerCalls(root);
            if (calls.Count == 0)
            {
                string assistantText = ComputerResponseParser.ExtractAssistantText(root);
                if (!string.IsNullOrWhiteSpace(assistantText))
                {
                    Console.WriteLine(assistantText.Trim());
                }

                Console.WriteLine($"Step {step}: no computer_call action returned; stopping.");
                logger.WriteJson("summary.json", new
                {
                    success = true,
                    state.StepCount,
                    finalResponseId = previousResponseId,
                    finalText = assistantText
                });
                stateStore.Delete();
                return 0;
            }

            var stepResults = new List<object>();
            string pendingCallId = calls[^1].CallId;
            foreach (ComputerCall call in calls)
            {
                pendingCallId = call.CallId;
                Console.WriteLine($"Step {step}: call_id={call.CallId}, actions={call.Actions.Count}");
                foreach (ComputerAction action in call.Actions)
                {
                    ActionExecutionResult result = ExecuteAction(action, screenshot.Display);
                    Console.WriteLine($"  {result.ActionType}: {(result.Success ? "ok" : "failed")} - {result.Message}");
                    stepResults.Add(new
                    {
                        call.CallId,
                        result.ActionType,
                        result.Success,
                        result.Message,
                        rawAction = JsonSerializer.Deserialize<object>(action.RawAction.GetRawText())
                    });
                }
            }

            logger.WriteJson($"actions-{step:000}.json", stepResults);
            ScreenshotFrame afterScreenshot = CaptureScreenshot(options, state);
            logger.WriteScreenshotDataUrl($"screenshot-after-{step:000}.png", afterScreenshot.DataUrl);

            state.PreviousResponseId = previousResponseId;
            state.PendingCallId = pendingCallId;
            state.StepCount = step;
            state.Display = afterScreenshot.Display;
            stateStore.Save(state);
            runSteps++;
        }

        if (state.StepCount >= options.MaxSteps)
        {
            Console.WriteLine($"Reached max steps ({options.MaxSteps}); stopping.");
            logger.WriteJson("summary.json", new { success = true, state.StepCount, reason = "max_steps" });
            return 0;
        }

        Console.WriteLine("PAUSE_RESUME");
        logger.WriteJson("summary.json", new { success = true, state.StepCount, reason = "max_steps_per_run" });
        return 0;
    }

    private static string RequirePendingCallId(LoopState state)
    {
        if (string.IsNullOrWhiteSpace(state.PendingCallId))
        {
            throw new InvalidOperationException("Previous response required a call_id but none was available.");
        }

        return state.PendingCallId;
    }

    private static string BuildDriverContext(DisplayInfo display)
    {
        string scope = string.IsNullOrWhiteSpace(display.Scope) ? "desktop" : display.Scope;
        string title = string.IsNullOrWhiteSpace(display.WindowTitle) ? string.Empty : $" Target window: \"{display.WindowTitle}\".";
        return $"The screenshot viewport is scoped to '{scope}'. Coordinates you return are relative to the screenshot image, not the full desktop.{title}";
    }

    private static ScreenshotFrame CaptureScreenshot(RunnerOptions options, LoopState state)
    {
        DisplayInfo display = ResolveCaptureDisplay(options, state);
        using var bitmap = new Bitmap(display.EffectiveCaptureWidth, display.EffectiveCaptureHeight);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(display.Left, display.Top, 0, 0, new Size(display.EffectiveCaptureWidth, display.EffectiveCaptureHeight));
        }

        using Bitmap finalBitmap = options.ScreenshotScale > 0 && Math.Abs(options.ScreenshotScale - 1.0) > 0.001
            ? ResizeBitmap(bitmap, options.ScreenshotScale)
            : (Bitmap)bitmap.Clone();

        DisplayInfo screenshotDisplay = new()
        {
            Left = display.Left,
            Top = display.Top,
            Width = finalBitmap.Width,
            Height = finalBitmap.Height,
            CaptureWidth = display.EffectiveCaptureWidth,
            CaptureHeight = display.EffectiveCaptureHeight,
            VirtualDesktopLeft = display.EffectiveVirtualDesktopLeft,
            VirtualDesktopTop = display.EffectiveVirtualDesktopTop,
            VirtualDesktopWidth = display.EffectiveVirtualDesktopWidth,
            VirtualDesktopHeight = display.EffectiveVirtualDesktopHeight,
            Scope = display.Scope,
            WindowTitle = display.WindowTitle
        };

        using var stream = new MemoryStream();
        finalBitmap.Save(stream, ImageFormat.Png);
        string dataUrl = $"data:image/png;base64,{Convert.ToBase64String(stream.ToArray())}";
        return new ScreenshotFrame(dataUrl, screenshotDisplay);
    }

    private static DisplayInfo ResolveCaptureDisplay(RunnerOptions options, LoopState state)
    {
        VirtualDesktopInfo virtualDesktop = GetVirtualDesktopInfo();
        if (string.Equals(options.CaptureScope, "desktop", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDesktopDisplay(virtualDesktop);
        }

        WindowTarget target = ResolvePersistedWindowTarget(state);
        string resolutionSource = "persisted";
        if (target.Handle == IntPtr.Zero)
        {
            target = ResolveWindowTarget(options);
            resolutionSource = "search";
        }

        if (target.Handle == IntPtr.Zero)
        {
            string saved = state.TargetWindow == null
                ? "none"
                : $"handle={state.TargetWindow.Handle}; processId={state.TargetWindow.ProcessId}; initialTitle='{state.TargetWindow.InitialTitle}'; lastTitle='{state.TargetWindow.LastTitle}'; process='{state.TargetWindow.ProcessName}'";
            throw new InvalidOperationException($"Capture scope '{options.CaptureScope}' requires a target window but none was available. WindowTitle='{options.WindowTitle}'; ProcessName='{options.ProcessName}'; savedTarget={saved}.");
        }

        IntPtr captureHandle = ResolvePopupHandle(target, options.PopupMode);
        EnsureForeground(captureHandle);

        string initialTitle = state.TargetWindow?.InitialTitle ?? target.Title;
        WindowTarget captureTarget = target.Handle == captureHandle
            ? target
            : new WindowTarget(captureHandle, target.ProcessId, ReadWindowTitle(captureHandle), target.ProcessName);

        DisplayInfo display;
        if (string.Equals(options.CaptureScope, "client", StringComparison.OrdinalIgnoreCase)
            && TryGetClientDisplay(captureHandle, virtualDesktop, out DisplayInfo clientDisplay))
        {
            display = clientDisplay;
        }
        else
        {
            if (!GetWindowRect(captureHandle, out RECT rect))
            {
                throw new InvalidOperationException($"Could not read window rectangle for {resolutionSource} target handle={captureHandle}. Win32Error={Marshal.GetLastWin32Error()}.");
            }

            string resolvedScope = string.Equals(options.CaptureScope, "client", StringComparison.OrdinalIgnoreCase)
                ? "window"
                : options.CaptureScope;
            display = CreateWindowDisplay(rect, virtualDesktop, resolvedScope, ReadWindowTitle(captureHandle));
        }

        PersistWindowTarget(state, captureTarget, initialTitle);
        return display;
    }
    private static DisplayInfo CreateDesktopDisplay(VirtualDesktopInfo virtualDesktop)
    {
        var display = new DisplayInfo
        {
            Left = virtualDesktop.Left,
            Top = virtualDesktop.Top,
            Width = virtualDesktop.Width,
            Height = virtualDesktop.Height,
            CaptureWidth = virtualDesktop.Width,
            CaptureHeight = virtualDesktop.Height,
            VirtualDesktopLeft = virtualDesktop.Left,
            VirtualDesktopTop = virtualDesktop.Top,
            VirtualDesktopWidth = virtualDesktop.Width,
            VirtualDesktopHeight = virtualDesktop.Height,
            Scope = "desktop"
        };

        if (!display.IsValid)
        {
            throw new InvalidOperationException($"Invalid virtual desktop geometry: {display.Width}x{display.Height} at ({display.Left},{display.Top}).");
        }

        return display;
    }

    private static DisplayInfo CreateWindowDisplay(RECT rect, VirtualDesktopInfo virtualDesktop, string scope, string windowTitle)
    {
        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        return new DisplayInfo
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = width,
            Height = height,
            CaptureWidth = width,
            CaptureHeight = height,
            VirtualDesktopLeft = virtualDesktop.Left,
            VirtualDesktopTop = virtualDesktop.Top,
            VirtualDesktopWidth = virtualDesktop.Width,
            VirtualDesktopHeight = virtualDesktop.Height,
            Scope = scope,
            WindowTitle = windowTitle
        };
    }

    private static Bitmap ResizeBitmap(Bitmap source, double scale)
    {
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, width, height);
        return resized;
    }

    private static bool TryGetClientDisplay(IntPtr handle, VirtualDesktopInfo virtualDesktop, out DisplayInfo display)
    {
        display = new DisplayInfo();
        if (!GetClientRect(handle, out RECT clientRect))
        {
            return false;
        }

        int width = clientRect.Right - clientRect.Left;
        int height = clientRect.Bottom - clientRect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var topLeft = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(handle, ref topLeft))
        {
            return false;
        }

        display = new DisplayInfo
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Width = width,
            Height = height,
            CaptureWidth = width,
            CaptureHeight = height,
            VirtualDesktopLeft = virtualDesktop.Left,
            VirtualDesktopTop = virtualDesktop.Top,
            VirtualDesktopWidth = virtualDesktop.Width,
            VirtualDesktopHeight = virtualDesktop.Height,
            Scope = "client",
            WindowTitle = ReadWindowTitle(handle)
        };
        return true;
    }

    private static WindowTarget ResolvePersistedWindowTarget(LoopState state)
    {
        TargetWindowInfo? saved = state.TargetWindow;
        if (saved == null || saved.Handle == 0)
        {
            return default;
        }

        IntPtr handle = new(saved.Handle);
        if (!IsWindow(handle) || !IsWindowVisible(handle) || !GetWindowRect(handle, out RECT rect))
        {
            return default;
        }

        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
        {
            return default;
        }

        GetWindowThreadProcessId(handle, out uint processId);
        if (saved.ProcessId != 0 && processId != saved.ProcessId)
        {
            return default;
        }

        string title = ReadWindowTitle(handle);
        string processName = ReadProcessName(processId);
        return new WindowTarget(handle, processId, title, processName);
    }

    private static void PersistWindowTarget(LoopState state, WindowTarget target, string initialTitle)
    {
        if (target.Handle == IntPtr.Zero)
        {
            return;
        }

        state.TargetWindow = new TargetWindowInfo
        {
            Handle = target.Handle.ToInt64(),
            ProcessId = target.ProcessId,
            InitialTitle = string.IsNullOrWhiteSpace(initialTitle) ? target.Title : initialTitle,
            LastTitle = target.Title,
            ProcessName = target.ProcessName,
            ResolvedUtc = state.TargetWindow?.ResolvedUtc ?? DateTimeOffset.UtcNow,
            LastUsedUtc = DateTimeOffset.UtcNow
        };
    }
    private static WindowTarget ResolveWindowTarget(RunnerOptions options)
    {
        var matches = new List<WindowTarget>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || !GetWindowRect(handle, out RECT rect))
            {
                return true;
            }

            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            {
                return true;
            }

            string title = ReadWindowTitle(handle);
            GetWindowThreadProcessId(handle, out uint processId);
            string processName = ReadProcessName(processId);
            if (MatchesWindow(options, title, processName))
            {
                matches.Add(new WindowTarget(handle, processId, title, processName));
            }

            return true;
        }, IntPtr.Zero);

        return matches
            .OrderByDescending(match => !string.IsNullOrWhiteSpace(options.WindowTitle)
                && match.Title.Contains(options.WindowTitle, StringComparison.OrdinalIgnoreCase))
            .ThenBy(match => match.Title.Length)
            .FirstOrDefault();
    }

    private static bool MatchesWindow(RunnerOptions options, string title, string processName)
    {
        bool titleMatches = string.IsNullOrWhiteSpace(options.WindowTitle)
            || title.Contains(options.WindowTitle, StringComparison.OrdinalIgnoreCase);
        bool processMatches = string.IsNullOrWhiteSpace(options.ProcessName)
            || string.Equals(processName, NormalizeProcessName(options.ProcessName), StringComparison.OrdinalIgnoreCase);
        return titleMatches && processMatches;
    }

    private static IntPtr ResolvePopupHandle(WindowTarget target, string popupMode)
    {
        if (!string.Equals(popupMode, "active-owned", StringComparison.OrdinalIgnoreCase))
        {
            return target.Handle;
        }

        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == target.Handle)
        {
            return target.Handle;
        }

        GetWindowThreadProcessId(foreground, out uint foregroundProcessId);
        if (foregroundProcessId == target.ProcessId)
        {
            return foreground;
        }

        return target.Handle;
    }

    private static void EnsureForeground(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
            Thread.Sleep(150);
        }

        SetForegroundWindow(handle);
        Thread.Sleep(120);
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        int length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadProcessName(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeProcessName(string value)
    {
        string name = value.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(name)
            : name;
    }

    private static VirtualDesktopInfo GetVirtualDesktopInfo()
    {
        return new VirtualDesktopInfo(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));
    }

    private static ActionExecutionResult ExecuteAction(ComputerAction action, DisplayInfo display)
    {
        try
        {
            return action.ActionType.ToLowerInvariant() switch
            {
                "screenshot" => Ok(action.ActionType, "Screenshot requested."),
                "click" => ExecuteClick(action, display, doubleClick: false),
                "double_click" => ExecuteClick(action, display, doubleClick: true),
                "move" => ExecuteMove(action, display),
                "drag" => ExecuteDrag(action, display),
                "scroll" => ExecuteScroll(action, display),
                "keypress" => ExecuteKeypress(action),
                "type" => ExecuteType(action),
                "wait" => ExecuteWait(action),
                _ => Fail(action.ActionType, $"Unsupported action '{action.ActionType}'.")
            };
        }
        catch (Exception ex)
        {
            return Fail(action.ActionType, ex.Message);
        }
    }

    private static ActionExecutionResult ExecuteMove(ComputerAction action, DisplayInfo display)
    {
        CoordinateMapResult mapped = CoordinateMapper.Map(action.RawAction, display);
        if (!mapped.Success)
        {
            return Fail(action.ActionType, mapped.Message);
        }

        SendMouseMove(mapped.AbsoluteX, mapped.AbsoluteY, display);
        return Ok(action.ActionType, $"Moved mouse to ({mapped.AbsoluteX},{mapped.AbsoluteY}).");
    }

    private static ActionExecutionResult ExecuteClick(ComputerAction action, DisplayInfo display, bool doubleClick)
    {
        CoordinateMapResult mapped = CoordinateMapper.Map(action.RawAction, display);
        if (!mapped.Success)
        {
            return Fail(action.ActionType, mapped.Message);
        }

        SendMouseMove(mapped.AbsoluteX, mapped.AbsoluteY, display);
        string button = ReadString(action.RawAction, "button") ?? "left";
        (uint down, uint up) = ResolveMouseButton(button);
        int count = doubleClick ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            SendMouseButton(down);
            SendMouseButton(up);
            Thread.Sleep(60);
        }

        return Ok(action.ActionType, $"{(doubleClick ? "Double-clicked" : "Clicked")} {button} at ({mapped.AbsoluteX},{mapped.AbsoluteY}).");
    }

    private static ActionExecutionResult ExecuteDrag(ComputerAction action, DisplayInfo display)
    {
        if (!TryGetCoordinate(action.RawAction, "x", "from_x", out int startX)
            || !TryGetCoordinate(action.RawAction, "y", "from_y", out int startY)
            || !TryGetCoordinate(action.RawAction, "to_x", "target_x", out int endX)
            || !TryGetCoordinate(action.RawAction, "to_y", "target_y", out int endY))
        {
            return Fail(action.ActionType, "Drag action is missing start or target coordinates.");
        }

        var startAction = JsonDocument.Parse($$"""{ "x": {{startX}}, "y": {{startY}} }""").RootElement.Clone();
        var endAction = JsonDocument.Parse($$"""{ "x": {{endX}}, "y": {{endY}} }""").RootElement.Clone();
        CoordinateMapResult start = CoordinateMapper.Map(startAction, display);
        CoordinateMapResult end = CoordinateMapper.Map(endAction, display);
        if (!start.Success)
        {
            return Fail(action.ActionType, start.Message);
        }

        if (!end.Success)
        {
            return Fail(action.ActionType, end.Message);
        }

        SendMouseMove(start.AbsoluteX, start.AbsoluteY, display);
        SendMouseButton(MouseeventfLeftdown);
        const int steps = 12;
        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            int nextX = (int)Math.Round(start.AbsoluteX + ((end.AbsoluteX - start.AbsoluteX) * t));
            int nextY = (int)Math.Round(start.AbsoluteY + ((end.AbsoluteY - start.AbsoluteY) * t));
            SendMouseMove(nextX, nextY, display);
            Thread.Sleep(15);
        }

        SendMouseButton(MouseeventfLeftup);
        return Ok(action.ActionType, $"Dragged from ({start.AbsoluteX},{start.AbsoluteY}) to ({end.AbsoluteX},{end.AbsoluteY}).");
    }

    private static ActionExecutionResult ExecuteScroll(ComputerAction action, DisplayInfo display)
    {
        if (CoordinateMapper.TryGetInt(action.RawAction, "x", out _) && CoordinateMapper.TryGetInt(action.RawAction, "y", out _))
        {
            ActionExecutionResult move = ExecuteMove(action, display);
            if (!move.Success)
            {
                return move;
            }
        }

        int vertical = ReadInt(action.RawAction, "scroll_y")
            ?? ReadInt(action.RawAction, "delta_y")
            ?? 0;
        int horizontal = ReadInt(action.RawAction, "scroll_x")
            ?? ReadInt(action.RawAction, "delta_x")
            ?? 0;

        if (vertical != 0)
        {
            SendMouseWheel(MouseeventfWheel, vertical);
        }

        if (horizontal != 0)
        {
            SendMouseWheel(MouseeventfHwheel, horizontal);
        }

        return Ok(action.ActionType, $"Scrolled x={horizontal}, y={vertical}.");
    }

    private static ActionExecutionResult ExecuteKeypress(ComputerAction action)
    {
        List<string> keys = ReadKeys(action.RawAction);
        if (keys.Count == 0)
        {
            return Fail(action.ActionType, "Keypress action did not contain keys.");
        }

        List<ushort> virtualKeys = new();
        foreach (string key in keys)
        {
            if (!TryResolveVirtualKey(key, out ushort vk))
            {
                return Fail(action.ActionType, $"Unsupported key '{key}'.");
            }

            virtualKeys.Add(vk);
        }

        foreach (ushort vk in virtualKeys)
        {
            SendKey(vk, keyUp: false);
        }

        for (int i = virtualKeys.Count - 1; i >= 0; i--)
        {
            SendKey(virtualKeys[i], keyUp: true);
        }

        Thread.Sleep(150);
        return Ok(action.ActionType, $"Pressed {string.Join("+", keys)}.");
    }

    private static ActionExecutionResult ExecuteType(ComputerAction action)
    {
        string? text = ReadString(action.RawAction, "text");
        if (string.IsNullOrEmpty(text))
        {
            return Fail(action.ActionType, "Type action did not contain text.");
        }

        string sanitizedText = SanitizeTypedText(text);
        string suffix = sanitizedText.Length == text.Length && string.Equals(sanitizedText, text, StringComparison.Ordinal)
            ? string.Empty
            : $" Sanitized from {text.Length} input character(s).";

        if (sanitizedText.Length > 1 && TryPasteText(sanitizedText))
        {
            return Ok(action.ActionType, $"Pasted {sanitizedText.Length} character(s)." + suffix);
        }

        foreach (char ch in sanitizedText)
        {
            SendUnicodeChar(ch);
            Thread.Sleep(20);
        }

        return Ok(action.ActionType, $"Typed {sanitizedText.Length} character(s)." + suffix);
    }
    private static string SanitizeTypedText(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (TryAppendMalformedAstralCodePoint(text, ref i, builder))
            {
                continue;
            }

            switch (ch)
            {
                case '\r':
                case '\n':
                case '\t':
                    builder.Append(ch);
                    break;
                case '\u0019':
                case '\u2018':
                case '\u2019':
                case '\u201B':
                    builder.Append('\'');
                    break;
                case '\u0014':
                case '\u2010':
                case '\u2011':
                case '\u2012':
                case '\u2013':
                case '\u2014':
                case '\u2212':
                    builder.Append('-');
                    break;
                case '\u201C':
                case '\u201D':
                    builder.Append('"');
                    break;
                default:
                    if (!char.IsControl(ch))
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryAppendMalformedAstralCodePoint(string text, ref int index, StringBuilder builder)
    {
        char prefix = text[index];
        if (prefix < '\u0001' || prefix > '\u0010' || index + 4 >= text.Length)
        {
            return false;
        }

        int value = prefix;
        for (int offset = 1; offset <= 4; offset++)
        {
            int digit = HexValue(text[index + offset]);
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) + digit;
        }

        if (value < 0x10000 || value > 0x10FFFF)
        {
            return false;
        }

        builder.Append(char.ConvertFromUtf32(value));
        index += 4;
        return true;
    }

    private static int HexValue(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }

        if (ch >= 'a' && ch <= 'f')
        {
            return ch - 'a' + 10;
        }

        if (ch >= 'A' && ch <= 'F')
        {
            return ch - 'A' + 10;
        }

        return -1;
    }

    private static bool TryPasteText(string text)
    {
        string? previousClipboard = null;
        bool hadText = false;
        bool clipboardReady = false;
        var thread = new Thread(() =>
        {
            try
            {
                hadText = Clipboard.ContainsText();
                previousClipboard = hadText ? Clipboard.GetText() : null;
                Clipboard.SetText(text);
                clipboardReady = true;
            }
            catch
            {
                clipboardReady = false;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (!clipboardReady)
        {
            return false;
        }

        SendKey(0x11, keyUp: false);
        SendKey(0x56, keyUp: false);
        SendKey(0x56, keyUp: true);
        SendKey(0x11, keyUp: true);
        Thread.Sleep(80);

        var restoreThread = new Thread(() =>
        {
            try
            {
                if (hadText && previousClipboard != null)
                {
                    Clipboard.SetText(previousClipboard);
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // Clipboard restoration is best effort.
            }
        });
        restoreThread.SetApartmentState(ApartmentState.STA);
        restoreThread.Start();
        restoreThread.Join();
        return true;
    }
    private static ActionExecutionResult ExecuteWait(ComputerAction action)
    {
        int delayMs = ReadInt(action.RawAction, "milliseconds")
            ?? ReadInt(action.RawAction, "duration")
            ?? ReadInt(action.RawAction, "ms")
            ?? 500;
        delayMs = Math.Max(0, delayMs);
        Thread.Sleep(delayMs);
        return Ok(action.ActionType, $"Waited {delayMs} ms.");
    }

    private static ActionExecutionResult Ok(string actionType, string message)
    {
        return new ActionExecutionResult { ActionType = actionType, Success = true, Message = message };
    }

    private static ActionExecutionResult Fail(string actionType, string message)
    {
        return new ActionExecutionResult { ActionType = actionType, Success = false, Message = message };
    }

    private static (uint Down, uint Up) ResolveMouseButton(string button)
    {
        return button.ToLowerInvariant() switch
        {
            "right" => (MouseeventfRightdown, MouseeventfRightup),
            "middle" => (MouseeventfMiddledown, MouseeventfMiddleup),
            _ => (MouseeventfLeftdown, MouseeventfLeftup)
        };
    }

    private static void SendMouseMove(int absoluteX, int absoluteY, DisplayInfo display)
    {
        int virtualLeft = display.EffectiveVirtualDesktopLeft;
        int virtualTop = display.EffectiveVirtualDesktopTop;
        int virtualWidth = display.EffectiveVirtualDesktopWidth;
        int virtualHeight = display.EffectiveVirtualDesktopHeight;
        int normalizedX = virtualWidth <= 1
            ? 0
            : (int)Math.Round(((absoluteX - virtualLeft) * 65535.0) / (virtualWidth - 1));
        int normalizedY = virtualHeight <= 1
            ? 0
            : (int)Math.Round(((absoluteY - virtualTop) * 65535.0) / (virtualHeight - 1));

        SendMouseInput(normalizedX, normalizedY, 0, MouseeventfMove | MouseeventfAbsolute | MouseeventfVirtualdesk);
    }

    private static void SendMouseButton(uint flag)
    {
        SendMouseInput(0, 0, 0, flag);
    }

    private static void SendMouseWheel(uint wheelFlag, int amount)
    {
        SendMouseInput(0, 0, amount, wheelFlag);
    }

    private static void SendMouseInput(int dx, int dy, int mouseData, uint flags)
    {
        INPUT[] inputs =
        {
            new()
            {
                type = InputMouse,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = dx,
                        dy = dy,
                        mouseData = mouseData,
                        dwFlags = flags
                    }
                }
            }
        };

        EnsureSendInput(inputs);
    }

    private static void SendKey(ushort virtualKey, bool keyUp)
    {
        INPUT[] inputs =
        {
            new()
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        dwFlags = keyUp ? KeyeventfKeyup : 0
                    }
                }
            }
        };

        EnsureSendInput(inputs);
    }

    private static void SendUnicodeChar(char ch)
    {
        INPUT[] inputs =
        {
            new()
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = ch,
                        dwFlags = KeyeventfUnicode
                    }
                }
            },
            new()
            {
                type = InputKeyboard,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wScan = ch,
                        dwFlags = KeyeventfUnicode | KeyeventfKeyup
                    }
                }
            }
        };

        EnsureSendInput(inputs);
    }

    private static void EnsureSendInput(INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput sent {sent} of {inputs.Length} event(s). Win32Error={Marshal.GetLastWin32Error()}.");
        }
    }

    private static List<string> ReadKeys(JsonElement action)
    {
        var keys = new List<string>();
        if (action.ValueKind == JsonValueKind.Object
            && action.TryGetProperty("keys", out JsonElement keyArray)
            && keyArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement key in keyArray.EnumerateArray())
            {
                if (key.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(key.GetString()))
                {
                    keys.Add(key.GetString()!.Trim());
                }
            }

            return keys;
        }

        string? keyOrChord = ReadString(action, "key") ?? ReadString(action, "chord");
        if (!string.IsNullOrWhiteSpace(keyOrChord))
        {
            keys.AddRange(keyOrChord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return keys;
    }

    private static bool TryResolveVirtualKey(string key, out ushort virtualKey)
    {
        if (SpecialKeys.TryGetValue(key, out virtualKey))
        {
            return true;
        }

        if (key.Length == 1)
        {
            short scan = VkKeyScan(key[0]);
            if (scan != -1)
            {
                virtualKey = (ushort)(scan & 0xFF);
                return true;
            }
        }

        if (key.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key[1..], out int fn)
            && fn >= 1
            && fn <= 24)
        {
            virtualKey = (ushort)(0x70 + (fn - 1));
            return true;
        }

        virtualKey = 0;
        return false;
    }

    private static bool TryGetCoordinate(JsonElement action, string primaryName, string alternateName, out int value)
    {
        return CoordinateMapper.TryGetInt(action, primaryName, out value)
            || CoordinateMapper.TryGetInt(action, alternateName, out value);
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        return CoordinateMapper.TryGetInt(element, propertyName, out int value) ? value : null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly record struct ScreenshotFrame(string DataUrl, DisplayInfo Display);
    private readonly record struct VirtualDesktopInfo(int Left, int Top, int Width, int Height);
    private readonly record struct WindowTarget(IntPtr Handle, uint ProcessId, string Title, string ProcessName);

    private sealed class RunnerOptions
    {
        public string Model { get; init; } = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? ComputerUseDefaults.Model;
        public string Endpoint { get; init; } = Environment.GetEnvironmentVariable("OPENAI_RESPONSES_ENDPOINT") ?? ComputerUseDefaults.Endpoint;
        public int MaxSteps { get; init; } = ParsePositiveInt(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_MAX_STEPS"), ComputerUseDefaults.MaxSteps);
        public int MaxStepsPerRun { get; init; } = ParsePositiveInt(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_MAX_STEPS_PER_RUN"), ComputerUseDefaults.MaxStepsPerRun);
        public string StateFile { get; init; } = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_STATE_FILE") ?? Path.Combine(Environment.CurrentDirectory, ComputerUseDefaults.StateFileName);
        public string? LogDirectory { get; init; } = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_LOG_DIR") ?? Path.Combine(Environment.CurrentDirectory, ComputerUseDefaults.LogDirectoryName);
        public double ScreenshotScale { get; init; } = ParsePositiveDouble(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_SCREENSHOT_SCALE"), 1.0);
        public string CaptureScope { get; init; } = NormalizeCaptureScope(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_CAPTURE_SCOPE"));
        public string WindowTitle { get; init; } = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_WINDOW_TITLE") ?? string.Empty;
        public string ProcessName { get; init; } = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_PROCESS_NAME") ?? string.Empty;
        public string PopupMode { get; init; } = NormalizePopupMode(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_POPUP_MODE"));
        public bool Reset { get; init; }
        public bool ShowHelp { get; init; }
        public string Instruction { get; init; } = string.Empty;

        public static string HelpText =>
            "Usage: Buffaly.OpenAI.ComputerUse.Runner [options] <instruction>\n"
            + "Options:\n"
            + "  --model <name>\n"
            + "  --endpoint <url>\n"
            + "  --max-steps <n>\n"
            + "  --max-steps-per-run <n>\n"
            + "  --state-file <path>\n"
            + "  --log-dir <path>\n"
            + "  --screenshot-scale <number>\n"
            + "  --capture-scope <desktop|window|client>\n"
            + "  --window-title <substring>\n"
            + "  --process-name <name>\n"
            + "  --popup-mode <none|active-owned>\n"
            + "  --reset\n"
            + "  --help";

        public static RunnerOptions Parse(string[] args)
        {
            string model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? ComputerUseDefaults.Model;
            string endpoint = Environment.GetEnvironmentVariable("OPENAI_RESPONSES_ENDPOINT") ?? ComputerUseDefaults.Endpoint;
            int maxSteps = ParsePositiveInt(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_MAX_STEPS"), ComputerUseDefaults.MaxSteps);
            int maxStepsPerRun = ParsePositiveInt(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_MAX_STEPS_PER_RUN"), ComputerUseDefaults.MaxStepsPerRun);
            string stateFile = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_STATE_FILE") ?? Path.Combine(Environment.CurrentDirectory, ComputerUseDefaults.StateFileName);
            string? logDir = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_LOG_DIR") ?? Path.Combine(Environment.CurrentDirectory, ComputerUseDefaults.LogDirectoryName);
            double screenshotScale = ParsePositiveDouble(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_SCREENSHOT_SCALE"), 1.0);
            string captureScope = NormalizeCaptureScope(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_CAPTURE_SCOPE"));
            string windowTitle = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_WINDOW_TITLE") ?? string.Empty;
            string processName = Environment.GetEnvironmentVariable("OPENAI_COMPUTER_PROCESS_NAME") ?? string.Empty;
            string popupMode = NormalizePopupMode(Environment.GetEnvironmentVariable("OPENAI_COMPUTER_POPUP_MODE"));
            bool reset = false;
            bool help = false;
            var instruction = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--reset":
                        reset = true;
                        break;
                    case "--help":
                    case "-h":
                        help = true;
                        break;
                    case "--model":
                        model = RequireValue(args, ref i, arg);
                        break;
                    case "--endpoint":
                        endpoint = RequireValue(args, ref i, arg);
                        break;
                    case "--max-steps":
                        maxSteps = ParsePositiveInt(RequireValue(args, ref i, arg), ComputerUseDefaults.MaxSteps);
                        break;
                    case "--max-steps-per-run":
                        maxStepsPerRun = ParsePositiveInt(RequireValue(args, ref i, arg), ComputerUseDefaults.MaxStepsPerRun);
                        break;
                    case "--state-file":
                        stateFile = RequireValue(args, ref i, arg);
                        break;
                    case "--log-dir":
                        logDir = RequireValue(args, ref i, arg);
                        break;
                    case "--screenshot-scale":
                        screenshotScale = ParsePositiveDouble(RequireValue(args, ref i, arg), 1.0);
                        break;
                    case "--capture-scope":
                        captureScope = NormalizeCaptureScope(RequireValue(args, ref i, arg));
                        break;
                    case "--window-title":
                        windowTitle = RequireValue(args, ref i, arg);
                        break;
                    case "--process-name":
                        processName = RequireValue(args, ref i, arg);
                        break;
                    case "--popup-mode":
                        popupMode = NormalizePopupMode(RequireValue(args, ref i, arg));
                        break;
                    default:
                        instruction.Add(arg);
                        break;
                }
            }

            return new RunnerOptions
            {
                Model = model,
                Endpoint = endpoint,
                MaxSteps = maxSteps,
                MaxStepsPerRun = maxStepsPerRun,
                StateFile = stateFile,
                LogDirectory = logDir,
                ScreenshotScale = screenshotScale,
                CaptureScope = captureScope,
                WindowTitle = windowTitle,
                ProcessName = processName,
                PopupMode = popupMode,
                Reset = reset,
                ShowHelp = help,
                Instruction = string.Join(" ", instruction).Trim()
            };
        }

        private static string RequireValue(string[] args, ref int index, string name)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{name} requires a value.");
            }

            index++;
            return args[index];
        }

        private static int ParsePositiveInt(string? value, int fallback)
        {
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static double ParsePositiveDouble(string? value, double fallback)
        {
            return double.TryParse(value, out double parsed) && parsed > 0 ? parsed : fallback;
        }

        private static string NormalizeCaptureScope(string? value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "desktop" : value.Trim().ToLowerInvariant();
            return normalized is "desktop" or "window" or "client" ? normalized : "desktop";
        }

        private static string NormalizePopupMode(string? value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "active-owned" : value.Trim().ToLowerInvariant();
            return normalized is "none" or "active-owned" ? normalized : "active-owned";
        }
    }
}
