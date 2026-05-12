using System.Globalization;
using System.Text.Json;

namespace Buffaly.OpenAI.ComputerUse;

public static class CoordinateMapper
{
    public static CoordinateMapResult Map(JsonElement action, DisplayInfo display)
    {
        if (display == null || !display.IsValid)
        {
            return Fail("Display geometry is invalid.");
        }

        if (!TryGetInt(action, "x", out int x) || !TryGetInt(action, "y", out int y))
        {
            return Fail("Action is missing x/y coordinates.");
        }

        if (x < 0 || y < 0 || x >= display.Width || y >= display.Height)
        {
            return new CoordinateMapResult
            {
                Success = false,
                ScreenshotX = x,
                ScreenshotY = y,
                AbsoluteX = display.Left + x,
                AbsoluteY = display.Top + y,
                Message = $"Coordinate ({x},{y}) is outside screenshot bounds {display.Width}x{display.Height}."
            };
        }

        return new CoordinateMapResult
        {
            Success = true,
            ScreenshotX = x,
            ScreenshotY = y,
            AbsoluteX = display.Left + ScaleCoordinate(x, display.Width, display.EffectiveCaptureWidth),
            AbsoluteY = display.Top + ScaleCoordinate(y, display.Height, display.EffectiveCaptureHeight),
            Message = "Mapped screenshot coordinates to Windows desktop coordinates."
        };
    }

    private static int ScaleCoordinate(int value, int screenshotSize, int captureSize)
    {
        if (screenshotSize <= 1 || captureSize <= 1 || screenshotSize == captureSize)
        {
            return value;
        }

        return (int)Math.Round((value * (captureSize - 1.0)) / (screenshotSize - 1.0));
    }

    public static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement propertyValue))
        {
            return false;
        }

        if (propertyValue.ValueKind == JsonValueKind.Number)
        {
            if (propertyValue.TryGetInt32(out value))
            {
                return true;
            }

            if (propertyValue.TryGetDouble(out double asDouble))
            {
                value = (int)Math.Round(asDouble);
                return true;
            }
        }

        if (propertyValue.ValueKind == JsonValueKind.String
            && double.TryParse(propertyValue.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            value = (int)Math.Round(parsed);
            return true;
        }

        return false;
    }

    private static CoordinateMapResult Fail(string message)
    {
        return new CoordinateMapResult
        {
            Success = false,
            Message = message
        };
    }
}
