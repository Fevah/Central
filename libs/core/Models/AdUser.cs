namespace Central.Core.Models;

/// <summary>Active Directory user DTO — read-only snapshot from AD query.</summary>
public class AdUser
{
    public string ObjectGuid { get; set; } = "";
    public string SamAccountName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Company { get; set; } = "";
    public bool Enabled { get; set; }
    public string DistinguishedName { get; set; } = "";
    public string LoginName { get; set; } = "";

    /// <summary>True if this AD user is already linked to a Central user.</summary>
    public bool IsImported { get; set; }
}
