namespace Central.Engine.Models;

/// <summary>Tenant member row for detail dialog sub-grid.</summary>
public class TenantMemberRecord
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "Viewer";
    public DateTime JoinedAt { get; set; }
}

/// <summary>Plan dropdown item for AssignPlanDialog.</summary>
public class PlanItem
{
    public int Id { get; set; }
    public string Tier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? MaxUsers { get; set; }
    public int? MaxDevices { get; set; }
    public decimal? PriceMonthly { get; set; }
    public string DisplayText => $"{DisplayName} ({Tier})";
}

/// <summary>Module checklist item for GrantModuleDialog.</summary>
public class ModuleItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsBase { get; set; }
    public bool AlreadyGranted { get; set; }
    public string DisplayText => AlreadyGranted
        ? $"{DisplayName} ({Code}) [already granted]"
        : IsBase ? $"{DisplayName} ({Code}) [base]" : $"{DisplayName} ({Code})";
}

/// <summary>Tenant option for dropdown/multi-select lists.</summary>
public class TenantOption
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DisplayText => $"{DisplayName} ({Slug})";
}

/// <summary>Membership row for ManageMembershipsDialog grid.</summary>
public class MembershipRow : System.ComponentModel.INotifyPropertyChanged
{
    private string _role = "Viewer";

    public int Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = "";
    public string TenantName { get; set; } = "";
    public string Role
    {
        get => _role;
        set { _role = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Role))); }
    }
    public DateTime JoinedAt { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
