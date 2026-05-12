using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public static class OpenAIComputerUseFacade
{
    private const string Endpoint = "https://api.openai.com/v1/responses";
    private const int DefaultTimeoutSeconds = 180;
    private const int DefaultMaxOutputChars = 20000;
    private const int DefaultDisplayWidth = 1920;
    private const int DefaultDisplayHeight = 1080;
    private const string DefaultEnvironment = "windows";
    private const string DefaultModel = "gpt-5.4";
    private const string FallbackModelSuggestion = "computer-use-preview";

    public static string ExecuteTask(string apiKey, string instruction, string model, string timeoutSeconds, string maxOutputChars)
    {
        return CreateComputerGaInitialResponse(
            apiKey,
            instruction,
            model,
            timeoutSeconds,
            maxOutputChars,
            string.Empty);
    }

    public static string CreateComputerUseInitialResponse(
        string apiKey,
        string instruction,
        string model,
        string timeoutSeconds,
        string maxOutputChars,
        string environment,
        string displayWidth,
        string displayHeight,
        string screenshotDataUrl)
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string? validationError = ValidateRequired(
            ("apiKey", apiKey),
            ("instruction", instruction));

        if (validationError is not null)
        {
            return SerializeFailure(validationError, string.Empty, null, resolvedMaxOutputChars, null);
        }

        string resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        string resolvedEnvironment = string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment.Trim();
        int resolvedDisplayWidth = ParsePositiveInt(displayWidth, DefaultDisplayWidth);
        int resolvedDisplayHeight = ParsePositiveInt(displayHeight, DefaultDisplayHeight);
        int resolvedTimeoutSeconds = ParsePositiveInt(timeoutSeconds, DefaultTimeoutSeconds);
        string normalizedScreenshot;

        try
        {
            normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotDataUrl);
        }
        catch (Exception ex)
        {
            return SerializeFailure(
                ex.Message,
                string.Empty,
                null,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, ex.Message));
        }

        object payload = BuildInitialPayload(
            resolvedModel,
            instruction,
            resolvedEnvironment,
            resolvedDisplayWidth,
            resolvedDisplayHeight,
            normalizedScreenshot);

        ApiRequestResult requestResult = SendRequest(apiKey, payload, resolvedTimeoutSeconds, resolvedMaxOutputChars);
        if (!requestResult.Success)
        {
            return SerializeFailure(
                requestResult.Error ?? "Request failed.",
                requestResult.RequestBody,
                requestResult.StatusCode,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, requestResult.Error));
        }

        return SerializeSuccess(
            requestResult,
            resolvedMaxOutputChars,
            "initial",
            resolvedModel,
            BuildFallbackSuggestion(resolvedModel, null));
    }

    public static string CreateComputerUseFollowupResponse(
        string apiKey,
        string model,
        string previousResponseId,
        string callId,
        string timeoutSeconds,
        string maxOutputChars,
        string environment,
        string displayWidth,
        string displayHeight,
        string screenshotDataUrl)
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string? validationError = ValidateRequired(
            ("apiKey", apiKey),
            ("model", model),
            ("previousResponseId", previousResponseId),
            ("callId", callId));

        if (validationError is not null)
        {
            return SerializeFailure(validationError, string.Empty, null, resolvedMaxOutputChars, null);
        }

        string resolvedModel = model.Trim();
        string resolvedEnvironment = string.IsNullOrWhiteSpace(environment) ? DefaultEnvironment : environment.Trim();
        int resolvedDisplayWidth = ParsePositiveInt(displayWidth, DefaultDisplayWidth);
        int resolvedDisplayHeight = ParsePositiveInt(displayHeight, DefaultDisplayHeight);
        int resolvedTimeoutSeconds = ParsePositiveInt(timeoutSeconds, DefaultTimeoutSeconds);
        string normalizedScreenshot;

        try
        {
            normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotDataUrl);
        }
        catch (Exception ex)
        {
            return SerializeFailure(
                ex.Message,
                string.Empty,
                null,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, ex.Message));
        }

        object payload = BuildFollowUpPayload(
            resolvedModel,
            previousResponseId,
            callId,
            resolvedEnvironment,
            resolvedDisplayWidth,
            resolvedDisplayHeight,
            normalizedScreenshot);

        ApiRequestResult requestResult = SendRequest(apiKey, payload, resolvedTimeoutSeconds, resolvedMaxOutputChars);
        if (!requestResult.Success)
        {
            return SerializeFailure(
                requestResult.Error ?? "Request failed.",
                requestResult.RequestBody,
                requestResult.StatusCode,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, requestResult.Error));
        }

        return SerializeSuccess(
            requestResult,
            resolvedMaxOutputChars,
            "followup",
            resolvedModel,
            BuildFallbackSuggestion(resolvedModel, null));
    }

    public static string CreateComputerGaInitialResponse(
        string apiKey,
        string instruction,
        string modelName,
        string timeoutSeconds,
        string maxOutputChars,
        string screenshotInput,
        string apiBaseUrl = "https://api.openai.com/v1/responses")
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string? validationError = ValidateRequired(
            ("apiKey", apiKey),
            ("instruction", instruction));

        if (validationError is not null)
        {
            return SerializeFailure(validationError, string.Empty, null, resolvedMaxOutputChars, null);
        }

        string resolvedModel = string.IsNullOrWhiteSpace(modelName) ? DefaultModel : modelName.Trim();
        int resolvedTimeoutSeconds = ParsePositiveInt(timeoutSeconds, DefaultTimeoutSeconds);
        string resolvedEndpoint = string.IsNullOrWhiteSpace(apiBaseUrl) ? Endpoint : apiBaseUrl.Trim();
        string normalizedScreenshot;

        try
        {
            normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotInput);
        }
        catch (Exception ex)
        {
            return SerializeFailure(ex.Message, string.Empty, null, resolvedMaxOutputChars, null);
        }

        object payload = BuildGaInitialPayload(resolvedModel, instruction, normalizedScreenshot);
        ApiRequestResult requestResult = SendRequest(apiKey, payload, resolvedTimeoutSeconds, resolvedMaxOutputChars, resolvedEndpoint);
        if (!requestResult.Success)
        {
            return SerializeFailure(
                requestResult.Error ?? "Request failed.",
                requestResult.RequestBody,
                requestResult.StatusCode,
                resolvedMaxOutputChars,
                null);
        }

        return SerializeGaSuccess(requestResult, resolvedMaxOutputChars, "initial", resolvedModel);
    }

    public static string CreateComputerGaFollowupResponse(
        string apiKey,
        string modelName,
        string previousResponseId,
        string callId,
        string timeoutSeconds,
        string maxOutputChars,
        string screenshotInput,
        string apiBaseUrl = "https://api.openai.com/v1/responses")
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string? validationError = ValidateRequired(
            ("apiKey", apiKey),
            ("modelName", modelName),
            ("previousResponseId", previousResponseId),
            ("callId", callId),
            ("screenshotInput", screenshotInput));

        if (validationError is not null)
        {
            return SerializeFailure(validationError, string.Empty, null, resolvedMaxOutputChars, null);
        }

        string resolvedModel = modelName.Trim();
        int resolvedTimeoutSeconds = ParsePositiveInt(timeoutSeconds, DefaultTimeoutSeconds);
        string resolvedEndpoint = string.IsNullOrWhiteSpace(apiBaseUrl) ? Endpoint : apiBaseUrl.Trim();
        string normalizedScreenshot;

        try
        {
            normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotInput);
        }
        catch (Exception ex)
        {
            return SerializeFailure(ex.Message, string.Empty, null, resolvedMaxOutputChars, null);
        }

        object payload = BuildGaFollowUpPayload(resolvedModel, previousResponseId, callId, normalizedScreenshot);
        ApiRequestResult requestResult = SendRequest(apiKey, payload, resolvedTimeoutSeconds, resolvedMaxOutputChars, resolvedEndpoint);
        if (!requestResult.Success)
        {
            return SerializeFailure(
                requestResult.Error ?? "Request failed.",
                requestResult.RequestBody,
                requestResult.StatusCode,
                resolvedMaxOutputChars,
                null);
        }

        return SerializeGaSuccess(requestResult, resolvedMaxOutputChars, "followup", resolvedModel);
    }

    public static string ExtractComputerActions(string responseJson, string maxOutputChars)
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return SerializeFailure("responseJson is required.", string.Empty, null, resolvedMaxOutputChars, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(responseJson);
            JsonElement root = doc.RootElement;
            List<Dictionary<string, object?>> actions = ExtractComputerCallActions(root);
            if (actions.Count == 0)
            {
                actions = ExtractNormalizedActions(root);
            }

            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["success"] = true,
                ["statusCode"] = (int?)null,
                ["requestBody"] = string.Empty,
                ["error"] = null,
                ["responseId"] = ReadString(root, "id") ?? string.Empty,
                ["finalResponseSnippet"] = ExtractFinalResponseSnippet(root, resolvedMaxOutputChars),
                ["actionCount"] = actions.Count,
                ["actions"] = actions
            });
        }
        catch (Exception ex)
        {
            return SerializeFailure(ex.Message, string.Empty, null, resolvedMaxOutputChars, null);
        }
    }

    public static string CreateComputerUseInitialResponseFromFile(
        string apiKey,
        string instruction,
        string model,
        string timeoutSeconds,
        string maxOutputChars,
        string environment,
        string displayWidth,
        string displayHeight,
        string screenshotFilePath)
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();

        try
        {
            string normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotFilePath);
            return CreateComputerUseInitialResponse(
                apiKey,
                instruction,
                model,
                timeoutSeconds,
                maxOutputChars,
                environment,
                displayWidth,
                displayHeight,
                normalizedScreenshot);
        }
        catch (Exception ex)
        {
            return SerializeFailure(
                ex.Message,
                string.Empty,
                null,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, ex.Message));
        }
    }

    public static string CreateComputerUseFollowupResponseFromFile(
        string apiKey,
        string model,
        string previousResponseId,
        string callId,
        string timeoutSeconds,
        string maxOutputChars,
        string environment,
        string displayWidth,
        string displayHeight,
        string screenshotFilePath)
    {
        int resolvedMaxOutputChars = ParsePositiveInt(maxOutputChars, DefaultMaxOutputChars);
        string resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();

        try
        {
            string normalizedScreenshot = NormalizeScreenshotDataUrlOrFile(screenshotFilePath);
            return CreateComputerUseFollowupResponse(
                apiKey,
                model,
                previousResponseId,
                callId,
                timeoutSeconds,
                maxOutputChars,
                environment,
                displayWidth,
                displayHeight,
                normalizedScreenshot);
        }
        catch (Exception ex)
        {
            return SerializeFailure(
                ex.Message,
                string.Empty,
                null,
                resolvedMaxOutputChars,
                BuildFallbackSuggestion(resolvedModel, ex.Message));
        }
    }

    private static string NormalizeScreenshotDataUrlOrFile(string screenshotDataUrlOrFilePath)
    {
        if (string.IsNullOrWhiteSpace(screenshotDataUrlOrFilePath))
        {
            return string.Empty;
        }

        string value = screenshotDataUrlOrFilePath.Trim();
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!File.Exists(value))
        {
            throw new FileNotFoundException($"Screenshot file not found: {value}", value);
        }

        byte[] bytes = File.ReadAllBytes(value);
        string extension = Path.GetExtension(value).ToLowerInvariant();
        string mimeType = extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".png" => "image/png",
            _ => "image/png"
        };

        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static object BuildInitialPayload(
        string model,
        string instruction,
        string environment,
        int displayWidth,
        int displayHeight,
        string screenshotDataUrl)
    {
        var content = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "input_text",
                ["text"] = instruction.Trim()
            }
        };

        if (!string.IsNullOrWhiteSpace(screenshotDataUrl))
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "input_image",
                ["image_url"] = screenshotDataUrl.Trim()
            });
        }

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["truncation"] = "auto",
            ["tools"] = new object[]
            {
                BuildComputerTool(environment, displayWidth, displayHeight)
            },
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = content.ToArray()
                }
            }
        };
    }

    private static object BuildGaInitialPayload(
        string model,
        string instruction,
        string screenshotDataUrl)
    {
        return ComputerRequestBuilder.BuildInitialRequest(model, instruction, screenshotDataUrl);
    }

    private static object BuildFollowUpPayload(
        string model,
        string previousResponseId,
        string callId,
        string environment,
        int displayWidth,
        int displayHeight,
        string screenshotDataUrl)
    {
        var output = new Dictionary<string, object?>
        {
            ["type"] = "input_text",
            ["text"] = "Action output acknowledged."
        };

        if (!string.IsNullOrWhiteSpace(screenshotDataUrl))
        {
            output = new Dictionary<string, object?>
            {
                ["type"] = "input_image",
                ["image_url"] = screenshotDataUrl.Trim()
            };
        }

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["truncation"] = "auto",
            ["tools"] = new object[]
            {
                BuildComputerTool(environment, displayWidth, displayHeight)
            },
            ["previous_response_id"] = previousResponseId.Trim(),
            ["input"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "computer_call_output",
                    ["call_id"] = callId.Trim(),
                    ["output"] = output
                }
            }
        };
    }

    private static object BuildGaFollowUpPayload(
        string model,
        string previousResponseId,
        string callId,
        string screenshotDataUrl)
    {
        return ComputerRequestBuilder.BuildFollowUpRequest(model, previousResponseId, callId, screenshotDataUrl);
    }

    private static Dictionary<string, object?> BuildComputerTool(string environment, int displayWidth, int displayHeight)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "computer_use_preview",
            ["environment"] = environment,
            ["display_width"] = displayWidth,
            ["display_height"] = displayHeight
        };
    }

    private static ApiRequestResult SendRequest(string apiKey, object payload, int timeoutSeconds, int maxOutputChars)
    {
        return SendRequest(apiKey, payload, timeoutSeconds, maxOutputChars, Endpoint);
    }

    private static ApiRequestResult SendRequest(string apiKey, object payload, int timeoutSeconds, int maxOutputChars, string endpoint)
    {
        string requestBody = JsonSerializer.Serialize(payload);

        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return ApiRequestResult.Fail(
                    Snip(responseBody, maxOutputChars),
                    (int)response.StatusCode,
                    requestBody);
            }

            return ApiRequestResult.Ok(responseBody, (int)response.StatusCode, requestBody);
        }
        catch (Exception ex)
        {
            return ApiRequestResult.Fail(ex.Message, null, requestBody);
        }
    }

    private static string SerializeSuccess(
        ApiRequestResult requestResult,
        int maxOutputChars,
        string callType,
        string model,
        Dictionary<string, object?>? fallbackSuggestion)
    {
        using JsonDocument doc = JsonDocument.Parse(requestResult.ResponseBody ?? "{}");
        JsonElement root = doc.RootElement;
        List<Dictionary<string, object?>> actions = ExtractNormalizedActions(root);

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["statusCode"] = requestResult.StatusCode,
            ["requestBody"] = Snip(requestResult.RequestBody, maxOutputChars),
            ["error"] = null,
            ["callType"] = callType,
            ["model"] = model,
            ["responseId"] = ReadString(root, "id") ?? string.Empty,
            ["finalResponseSnippet"] = ExtractFinalResponseSnippet(root, maxOutputChars),
            ["actionCount"] = actions.Count,
            ["actions"] = actions,
            ["responseJson"] = Snip(requestResult.ResponseBody, maxOutputChars),
            ["fallbackSuggestion"] = fallbackSuggestion
        });
    }

    private static string SerializeFailure(
        string error,
        string requestBody,
        int? statusCode,
        int maxOutputChars,
        Dictionary<string, object?>? fallbackSuggestion)
    {
        var payload = new Dictionary<string, object?>
        {
            ["success"] = false,
            ["statusCode"] = statusCode,
            ["requestBody"] = Snip(requestBody, maxOutputChars),
            ["error"] = Snip(error, maxOutputChars),
            ["fallbackSuggestion"] = fallbackSuggestion
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string SerializeGaSuccess(
        ApiRequestResult requestResult,
        int maxOutputChars,
        string callType,
        string model)
    {
        using JsonDocument doc = JsonDocument.Parse(requestResult.ResponseBody ?? "{}");
        JsonElement root = doc.RootElement;
        Dictionary<string, object?> actionEnvelope = ParseGaActionEnvelope(root);

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["success"] = true,
            ["statusCode"] = requestResult.StatusCode,
            ["requestBody"] = Snip(requestResult.RequestBody, maxOutputChars),
            ["error"] = null,
            ["callType"] = callType,
            ["model"] = model,
            ["responseId"] = ReadString(root, "id") ?? string.Empty,
            ["finalResponseSnippet"] = ExtractFinalResponseSnippet(root, maxOutputChars),
            ["actionCount"] = actionEnvelope["actionCount"],
            ["actions"] = actionEnvelope["actions"],
            ["responseJson"] = Snip(requestResult.ResponseBody, maxOutputChars)
        });
    }

    private static Dictionary<string, object?> ParseGaActionEnvelope(JsonElement root)
    {
        var actions = new List<Dictionary<string, object?>>();
        if (!TryGetProperty(root, "output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, object?>
            {
                ["actionCount"] = 0,
                ["actions"] = actions
            };
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!string.Equals(ReadString(item, "type"), "computer_call", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string callId = ReadString(item, "call_id")
                ?? ReadString(item, "id")
                ?? string.Empty;

            if (!TryGetProperty(item, "action", out JsonElement actionElement) || actionElement.ValueKind != JsonValueKind.Object)
            {
                if (TryGetProperty(item, "actions", out JsonElement topLevelActionArray) && topLevelActionArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement oneAction in topLevelActionArray.EnumerateArray())
                    {
                        actions.Add(new Dictionary<string, object?>
                        {
                            ["callId"] = callId,
                            ["actionType"] = ResolveActionType(oneAction, "computer_call"),
                            ["rawAction"] = JsonSerializer.Deserialize<object>(oneAction.GetRawText())
                        });
                    }
                }

                continue;
            }

            if (TryGetProperty(actionElement, "actions", out JsonElement actionArray) && actionArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement oneAction in actionArray.EnumerateArray())
                {
                    actions.Add(new Dictionary<string, object?>
                    {
                        ["callId"] = callId,
                        ["actionType"] = ResolveActionType(oneAction, "computer_call"),
                        ["rawAction"] = JsonSerializer.Deserialize<object>(oneAction.GetRawText())
                    });
                }

                continue;
            }

            actions.Add(new Dictionary<string, object?>
            {
                ["callId"] = callId,
                ["actionType"] = ResolveActionType(actionElement, "computer_call"),
                ["rawAction"] = JsonSerializer.Deserialize<object>(actionElement.GetRawText())
            });
        }

        return new Dictionary<string, object?>
        {
            ["actionCount"] = actions.Count,
            ["actions"] = actions
        };
    }

    private static List<Dictionary<string, object?>> ExtractComputerCallActions(JsonElement root)
    {
        var actions = new List<Dictionary<string, object?>>();
        foreach (ComputerCall call in ComputerResponseParser.ParseComputerCalls(root))
        {
            foreach (ComputerAction action in call.Actions)
            {
                actions.Add(new Dictionary<string, object?>
                {
                    ["callId"] = call.CallId,
                    ["actionType"] = action.ActionType,
                    ["rawAction"] = JsonSerializer.Deserialize<object>(action.RawAction.GetRawText())
                });
            }
        }

        return actions;
    }

    private static List<Dictionary<string, object?>> ExtractNormalizedActions(JsonElement root)
    {
        var actions = new List<Dictionary<string, object?>>();
        if (!TryGetProperty(root, "output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return actions;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            string sourceType = ReadString(item, "type") ?? string.Empty;
            if (!LooksLikeActionContainer(item, sourceType))
            {
                continue;
            }

            if (!TryExtractActionPayload(item, out JsonElement actionElement))
            {
                continue;
            }

            string callId = ReadString(item, "call_id")
                ?? ReadString(item, "id")
                ?? Guid.NewGuid().ToString("N");

            actions.Add(new Dictionary<string, object?>
            {
                ["callId"] = callId,
                ["actionType"] = ResolveActionType(actionElement, sourceType),
                ["rawAction"] = JsonSerializer.Deserialize<object>(actionElement.GetRawText())
            });
        }

        return actions;
    }

    private static bool LooksLikeActionContainer(JsonElement item, string sourceType)
    {
        string normalizedType = sourceType.Trim().ToLowerInvariant();
        if (normalizedType.Contains("computer") || normalizedType.Contains("tool") || normalizedType.Contains("function"))
        {
            return true;
        }

        if (TryGetProperty(item, "action", out JsonElement actionProperty) && actionProperty.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        if (TryGetProperty(item, "arguments", out JsonElement argumentsProperty))
        {
            return argumentsProperty.ValueKind == JsonValueKind.Object || argumentsProperty.ValueKind == JsonValueKind.String;
        }

        return false;
    }

    private static bool TryExtractActionPayload(JsonElement item, out JsonElement action)
    {
        if (TryGetProperty(item, "action", out JsonElement actionNode) && actionNode.ValueKind == JsonValueKind.Object)
        {
            action = actionNode;
            return true;
        }

        if (TryGetProperty(item, "arguments", out JsonElement arguments))
        {
            if (arguments.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(arguments, "action", out JsonElement nestedAction) && nestedAction.ValueKind == JsonValueKind.Object)
                {
                    action = nestedAction;
                    return true;
                }

                action = arguments;
                return true;
            }

            if (arguments.ValueKind == JsonValueKind.String)
            {
                string argsText = arguments.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(argsText) && TryParseActionJson(argsText, out JsonElement parsedAction))
                {
                    action = parsedAction;
                    return true;
                }
            }
        }

        if (TryGetProperty(item, "payload", out JsonElement payload))
        {
            if (payload.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(payload, "action", out JsonElement nestedPayloadAction) && nestedPayloadAction.ValueKind == JsonValueKind.Object)
                {
                    action = nestedPayloadAction;
                    return true;
                }

                action = payload;
                return true;
            }

            if (payload.ValueKind == JsonValueKind.String)
            {
                string payloadText = payload.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(payloadText) && TryParseActionJson(payloadText, out JsonElement parsedPayloadAction))
                {
                    action = parsedPayloadAction;
                    return true;
                }
            }
        }

        string? topLevelType = ReadString(item, "name")
            ?? ReadString(item, "action")
            ?? ReadString(item, "type");

        if (!string.IsNullOrWhiteSpace(topLevelType))
        {
            using JsonDocument synthetic = JsonDocument.Parse($"{{\"type\":\"{EscapeJson(topLevelType)}\"}}");
            action = synthetic.RootElement.Clone();
            return true;
        }

        action = default;
        return false;
    }

    private static bool TryParseActionJson(string json, out JsonElement action)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "action", out JsonElement nestedAction) && nestedAction.ValueKind == JsonValueKind.Object)
            {
                action = nestedAction.Clone();
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                action = root.Clone();
                return true;
            }
        }
        catch
        {
            // Ignore malformed JSON action blocks.
        }

        action = default;
        return false;
    }

    private static string ResolveActionType(JsonElement action, string sourceType)
    {
        string? explicitType = ReadStringAny(action, "type", "action", "name", "event");
        if (!string.IsNullOrWhiteSpace(explicitType))
        {
            return explicitType.Trim();
        }

        string fallback = sourceType.Trim().ToLowerInvariant();
        if (fallback.Contains("double")) return "double_click";
        if (fallback.Contains("click")) return "click";
        if (fallback.Contains("move")) return "move";
        if (fallback.Contains("scroll")) return "scroll";
        if (fallback.Contains("key") || fallback.Contains("press")) return "press";
        if (fallback.Contains("type")) return "type";
        if (fallback.Contains("wait") || fallback.Contains("sleep")) return "wait";
        return "unknown";
    }

    private static string ExtractFinalResponseSnippet(JsonElement root, int maxChars)
    {
        string direct = ReadString(root, "output_text") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return Snip(direct, maxChars);
        }

        if (!TryGetProperty(root, "output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (JsonElement outputItem in output.EnumerateArray())
        {
            if (string.Equals(ReadString(outputItem, "type"), "message", StringComparison.OrdinalIgnoreCase))
            {
                AppendMessageContent(outputItem, builder);
                continue;
            }

            if (string.Equals(ReadString(outputItem, "type"), "output_text", StringComparison.OrdinalIgnoreCase))
            {
                string? text = ReadString(outputItem, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text);
                }
            }
        }

        return Snip(builder.ToString().Trim(), maxChars);
    }

    private static void AppendMessageContent(JsonElement messageItem, StringBuilder builder)
    {
        if (!TryGetProperty(messageItem, "content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement contentItem in content.EnumerateArray())
        {
            string? contentType = ReadString(contentItem, "type");
            if (!string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? text = ReadString(contentItem, "text");
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
        {
            return value.ToString();
        }

        return null;
    }

    private static string? ReadStringAny(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            string? value = ReadString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int ParsePositiveInt(string input, int fallback)
    {
        return int.TryParse(input, out int value) && value > 0 ? value : fallback;
    }

    private static string? ValidateRequired(params (string Name, string Value)[] required)
    {
        foreach ((string name, string value) in required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return $"{name} is required.";
            }
        }

        return null;
    }

    private static Dictionary<string, object?> BuildFallbackSuggestion(string model, string? error)
    {
        bool suggested = ShouldSuggestFallback(error, model);
        return new Dictionary<string, object?>
        {
            ["automaticRetryPerformed"] = false,
            ["suggested"] = suggested,
            ["recommendedModel"] = FallbackModelSuggestion,
            ["reason"] = suggested
                ? "Selected model may not support computer_use_preview."
                : "No automatic fallback retries are performed by this facade."
        };
    }

    private static bool ShouldSuggestFallback(string? errorText, string currentModel)
    {
        if (string.IsNullOrWhiteSpace(currentModel))
        {
            return false;
        }

        if (!currentModel.Trim().StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(errorText))
        {
            return true;
        }

        string normalizedError = errorText.ToLowerInvariant();
        return normalizedError.Contains("computer_use_preview") && normalizedError.Contains("not supported");
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string Snip(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        int limit = maxChars > 0 ? maxChars : DefaultMaxOutputChars;
        return text.Length <= limit ? text : text.Substring(0, limit) + "...(truncated)";
    }

    private sealed class ApiRequestResult
    {
        private ApiRequestResult(bool success, string? responseBody, string? error, int? statusCode, string requestBody)
        {
            Success = success;
            ResponseBody = responseBody;
            Error = error;
            StatusCode = statusCode;
            RequestBody = requestBody;
        }

        public bool Success { get; }
        public string? ResponseBody { get; }
        public string? Error { get; }
        public int? StatusCode { get; }
        public string RequestBody { get; }

        public static ApiRequestResult Ok(string responseBody, int statusCode, string requestBody)
        {
            return new ApiRequestResult(true, responseBody, null, statusCode, requestBody);
        }

        public static ApiRequestResult Fail(string error, int? statusCode, string requestBody)
        {
            return new ApiRequestResult(false, null, error, statusCode, requestBody);
        }
    }
}
