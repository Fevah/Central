namespace Central.Engine.Integration;

/// <summary>
/// Converts a source field value to a target field value during sync.
/// Pluggable — register converters by type name.
/// </summary>
public interface IFieldConverter
{
    /// <summary>Converter type identifier (e.g. "direct", "constant", "expression", "lookup").</summary>
    string ConverterType { get; }

    /// <summary>Convert a source value using the configured expression.</summary>
    object? Convert(object? sourceValue, string expression, ConvertContext context);
}

/// <summary>Context passed to converters for access to the full source record and lookup data.</summary>
public class ConvertContext
{
    public Dictionary<string, object?> SourceRecord { get; init; } = new();
    public Dictionary<string, object?> TargetRecord { get; init; } = new();
    public Func<string, string, object?>? LookupFunc { get; init; }
}

// ── Built-in Converter Implementations ────────────────────────────────

/// <summary>Pass-through — no transformation.</summary>
public class DirectConverter : IFieldConverter
{
    public string ConverterType => "direct";
    public object? Convert(object? sourceValue, string expression, ConvertContext context) => sourceValue;
}

/// <summary>Return a constant value regardless of source.</summary>
public class ConstantConverter : IFieldConverter
{
    public string ConverterType => "constant";
    public object? Convert(object? sourceValue, string expression, ConvertContext context) => expression;
}

/// <summary>Combine multiple source fields. Expression: "{field1} {field2}" with placeholders.</summary>
public class CombineConverter : IFieldConverter
{
    public string ConverterType => "combine";
    public object? Convert(object? sourceValue, string expression, ConvertContext context)
    {
        var result = expression;
        foreach (var kv in context.SourceRecord)
            result = result.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
        return result;
    }
}

/// <summary>Split a source value. Expression: "delimiter|index" (e.g. " |0" = first word).</summary>
public class SplitConverter : IFieldConverter
{
    public string ConverterType => "split";
    public object? Convert(object? sourceValue, string expression, ConvertContext context)
    {
        var parts = expression.Split('|', 2);
        if (parts.Length != 2 || sourceValue == null) return sourceValue;
        var delimiter = parts[0];
        if (!int.TryParse(parts[1], out var index)) return sourceValue;
        var split = sourceValue.ToString()?.Split(delimiter);
        return split != null && index < split.Length ? split[index] : sourceValue;
    }
}

/// <summary>Lookup value from another table/entity. Expression: "table.column=value_column".</summary>
public class LookupConverter : IFieldConverter
{
    public string ConverterType => "lookup";
    public object? Convert(object? sourceValue, string expression, ConvertContext context)
    {
        if (sourceValue == null || context.LookupFunc == null) return sourceValue;
        return context.LookupFunc(expression, sourceValue.ToString() ?? "");
    }
}

/// <summary>Format a date value. Expression is a .NET date format string (e.g. "yyyy-MM-dd").</summary>
public class DateFormatConverter : IFieldConverter
{
    public string ConverterType => "date_format";
    public object? Convert(object? sourceValue, string expression, ConvertContext context)
    {
        if (sourceValue is DateTime dt) return dt.ToString(expression);
        if (sourceValue is DateTimeOffset dto) return dto.ToString(expression);
        if (DateTime.TryParse(sourceValue?.ToString(), out var parsed)) return parsed.ToString(expression);
        return sourceValue;
    }
}

/// <summary>Evaluate a simple expression. Expression: C# string interpolation-style with field refs.</summary>
public class ExpressionConverter : IFieldConverter
{
    public string ConverterType => "expression";
    public object? Convert(object? sourceValue, string expression, ConvertContext context)
    {
        // Simple expression evaluator — supports field references and basic transforms
        var result = expression;
        result = result.Replace("$value", sourceValue?.ToString() ?? "");
        foreach (var kv in context.SourceRecord)
            result = result.Replace($"${kv.Key}", kv.Value?.ToString() ?? "");

        // Handle basic transforms
        if (result.StartsWith("upper:")) return result[6..].ToUpperInvariant();
        if (result.StartsWith("lower:")) return result[6..].ToLowerInvariant();
        if (result.StartsWith("trim:")) return result[5..].Trim();
        if (result.StartsWith("bool:")) return result[5..].Equals("true", StringComparison.OrdinalIgnoreCase);

        return result;
    }
}
