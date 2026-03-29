namespace Central.Core.Models;

/// <summary>Grid display settings persisted per-user per-panel.</summary>
public class GridSettings
{
    public int RowHeight { get; set; } = 25;
    public bool UseAlternatingRows { get; set; } = true;
    public bool ShowSummaryFooter { get; set; } = true;
    public bool ShowGroupPanel { get; set; }
    public bool ShowAutoFilterRow { get; set; } = true;
    public List<string>? ColumnOrder { get; set; }
    public Dictionary<string, double>? ColumnWidths { get; set; }
    public List<string>? HiddenColumns { get; set; }
}

/// <summary>Form field layout for detail forms.</summary>
public class FormLayout
{
    public List<string>? FieldOrder { get; set; }
    public List<string>? HiddenFields { get; set; }
    public List<FieldGroup>? Groups { get; set; }
}

public class FieldGroup
{
    public string Name { get; set; } = "";
    public List<string> Fields { get; set; } = new();
    public bool IsCollapsed { get; set; }
}

/// <summary>Cross-panel linking rule: selecting in source filters target.</summary>
public class LinkRule
{
    public string SourcePanel { get; set; } = "";
    public string TargetPanel { get; set; } = "";
    public string SourceField { get; set; } = "";
    public string TargetField { get; set; } = "";
    public bool FilterOnSelect { get; set; } = true;
}

/// <summary>DB-stored panel customization record.</summary>
public class PanelCustomizationRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string PanelName { get; set; } = "";
    public string SettingType { get; set; } = "";
    public string SettingKey { get; set; } = "";
    public string SettingJson { get; set; } = "{}";
}
