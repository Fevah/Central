namespace Central.Core.Models;

/// <summary>Active Directory connection configuration.</summary>
public class AdConfig
{
    public string Domain { get; set; } = "";
    public string OuFilter { get; set; } = "";
    public string ServiceAccount { get; set; } = "";
    public string ServicePassword { get; set; } = "";
    public bool UseSsl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Domain);
}
