namespace Central.Engine.Widgets;

/// <summary>
/// Holds text replacement tokens for [WidgetCommand] names.
/// Based on TotalLink's WidgetCommandData.
/// </summary>
public class WidgetCommandData
{
    public Dictionary<string, string> TextReplacements { get; } = new();

    /// <summary>Apply all text replacements to a template string.</summary>
    public string Apply(string template)
    {
        var result = template;
        foreach (var (key, value) in TextReplacements)
            result = result.Replace($"{{{key}}}", value);
        return result;
    }
}
