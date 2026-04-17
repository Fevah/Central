using System.Text.Json;

namespace Central.Security;

/// <summary>
/// Attribute-Based Access Control (ABAC) policy engine.
/// Evaluates security policies against a principal and resource to determine:
/// - Row-level: can the user see this record?
/// - Field-level: which fields are visible/hidden?
/// Policies loaded from DB, cached per-tenant.
/// </summary>
public class SecurityPolicyEngine
{
    private static SecurityPolicyEngine? _instance;
    public static SecurityPolicyEngine Instance => _instance ??= new();

    private readonly Dictionary<string, List<SecurityPolicy>> _policies = new();

    /// <summary>Load policies for a tenant. Call after tenant resolution.</summary>
    public void LoadPolicies(string tenantSlug, IEnumerable<SecurityPolicy> policies)
    {
        _policies[tenantSlug] = policies.Where(p => p.IsEnabled).OrderBy(p => p.Priority).ToList();
    }

    /// <summary>Check if a user can access a specific record (row-level).</summary>
    public bool CanAccessRow(string tenantSlug, string entityType, SecurityContext userContext, Dictionary<string, object?> recordAttributes)
    {
        if (!_policies.TryGetValue(tenantSlug, out var policies)) return true; // no policies = allow all

        var rowPolicies = policies.Where(p => p.EntityType == entityType && p.PolicyType == "row").ToList();
        if (rowPolicies.Count == 0) return true;

        foreach (var policy in rowPolicies)
        {
            if (EvaluateConditions(policy.Conditions, userContext, recordAttributes))
            {
                // Policy matched — check if it's allow or deny
                return policy.Effect == "allow";
            }
        }

        return true; // no matching policy = allow
    }

    /// <summary>Get the list of fields hidden from this user for an entity type.</summary>
    public HashSet<string> GetHiddenFields(string tenantSlug, string entityType, SecurityContext userContext)
    {
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!_policies.TryGetValue(tenantSlug, out var policies)) return hidden;

        var fieldPolicies = policies.Where(p => p.EntityType == entityType && p.PolicyType == "field").ToList();
        foreach (var policy in fieldPolicies)
        {
            if (EvaluateConditions(policy.Conditions, userContext, new()))
            {
                if (policy.HiddenFields != null)
                    foreach (var f in policy.HiddenFields) hidden.Add(f);
            }
        }

        return hidden;
    }

    /// <summary>Filter a record's fields based on security policies.</summary>
    public Dictionary<string, object?> FilterFields(string tenantSlug, string entityType,
        SecurityContext userContext, Dictionary<string, object?> record)
    {
        var hidden = GetHiddenFields(tenantSlug, entityType, userContext);
        if (hidden.Count == 0) return record;

        return record.Where(kv => !hidden.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static bool EvaluateConditions(Dictionary<string, string>? conditions, SecurityContext user, Dictionary<string, object?> record)
    {
        if (conditions == null || conditions.Count == 0) return true;

        foreach (var (key, expected) in conditions)
        {
            var actual = key switch
            {
                "role" => user.Role,
                "department" => user.Department,
                "security_clearance" => user.SecurityClearance,
                "username" => user.Username,
                _ => record.GetValueOrDefault(key)?.ToString()
            };

            if (expected.StartsWith("!"))
            {
                if (actual == expected[1..]) return false; // NOT match
            }
            else
            {
                if (actual != expected) return false; // exact match required
            }
        }

        return true; // all conditions met
    }
}

public class SecurityPolicy
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public string PolicyType { get; set; } = "row"; // row, field
    public string Effect { get; set; } = "allow"; // allow, deny
    public Dictionary<string, string>? Conditions { get; set; }
    public string[]? HiddenFields { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Security context for the current user — populated from JWT/auth.</summary>
public class SecurityContext
{
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public string Department { get; set; } = "";
    public string SecurityClearance { get; set; } = "internal"; // public, internal, confidential, restricted
}
