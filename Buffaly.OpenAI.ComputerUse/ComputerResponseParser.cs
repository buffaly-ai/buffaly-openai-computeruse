using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public static class ComputerResponseParser
{
    public static List<ComputerCall> ParseComputerCalls(string responseJson)
    {
        using JsonDocument doc = JsonDocument.Parse(responseJson);
        return ParseComputerCalls(doc.RootElement);
    }

    public static List<ComputerCall> ParseComputerCalls(JsonElement root)
    {
        var calls = new List<ComputerCall>();
        if (!TryGetArray(root, "output", out JsonElement output))
        {
            return calls;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!string.Equals(ReadString(item, "type"), "computer_call", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string callId = ReadString(item, "call_id") ?? ReadString(item, "id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(callId))
            {
                continue;
            }

            var actions = new List<ComputerAction>();
            AddActionsFromContainer(item, actions);
            if (actions.Count == 0)
            {
                continue;
            }

            calls.Add(new ComputerCall
            {
                CallId = callId,
                Actions = actions
            });
        }

        return calls;
    }

    public static string ExtractAssistantText(JsonElement root)
    {
        if (!TryGetArray(root, "output", out JsonElement output))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (JsonElement item in output.EnumerateArray())
        {
            string? itemType = ReadString(item, "type");
            if (string.Equals(itemType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                AddText(parts, ReadString(item, "text"));
                continue;
            }

            if (!string.Equals(itemType, "message", StringComparison.OrdinalIgnoreCase)
                || !TryGetArray(item, "content", out JsonElement content))
            {
                continue;
            }

            foreach (JsonElement contentItem in content.EnumerateArray())
            {
                string? contentType = ReadString(contentItem, "type");
                if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase))
                {
                    AddText(parts, ReadString(contentItem, "text"));
                }
            }
        }

        return string.Join(Environment.NewLine, parts);
    }

    public static string? ReadResponseId(JsonElement root)
    {
        return ReadString(root, "id");
    }

    private static void AddActionsFromContainer(JsonElement item, List<ComputerAction> actions)
    {
        if (TryGetArray(item, "actions", out JsonElement topLevelActions))
        {
            AddActionArray(topLevelActions, actions);
            return;
        }

        if (!TryGetProperty(item, "action", out JsonElement action))
        {
            return;
        }

        if (action.ValueKind == JsonValueKind.Object)
        {
            if (TryGetArray(action, "actions", out JsonElement nestedActions))
            {
                AddActionArray(nestedActions, actions);
                return;
            }

            AddActionObject(action, actions);
            return;
        }

        if (action.ValueKind == JsonValueKind.String)
        {
            string? actionType = action.GetString();
            if (!string.IsNullOrWhiteSpace(actionType))
            {
                actions.Add(new ComputerAction
                {
                    ActionType = actionType.Trim(),
                    RawAction = item.Clone()
                });
            }
        }
    }

    private static void AddActionArray(JsonElement actionArray, List<ComputerAction> actions)
    {
        foreach (JsonElement action in actionArray.EnumerateArray())
        {
            if (action.ValueKind == JsonValueKind.Object)
            {
                AddActionObject(action, actions);
            }
        }
    }

    private static void AddActionObject(JsonElement action, List<ComputerAction> actions)
    {
        string actionType = ReadString(action, "type") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return;
        }

        actions.Add(new ComputerAction
        {
            ActionType = actionType.Trim(),
            RawAction = action.Clone()
        });
    }

    private static void AddText(List<string> parts, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(text.Trim());
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value))
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
}
