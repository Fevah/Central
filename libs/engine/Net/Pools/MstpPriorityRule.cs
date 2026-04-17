using System.Text.Json.Nodes;
using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// A policy document describing how MSTP bridge priorities are assigned
/// within a scope. One rule can contain multiple ordered
/// <see cref="MstpPriorityRuleStep"/> rows; the first step whose match
/// expression passes wins and its <c>priority</c> is assigned.
/// </summary>
public class MstpPriorityRule : EntityBase
{
    public string RuleCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Region;
    public Guid? ScopeEntityId { get; set; }
}

/// <summary>
/// One clause of a <see cref="MstpPriorityRule"/>. Evaluated in
/// <see cref="StepOrder"/> order; the first matching step assigns its
/// <see cref="Priority"/>.
/// <see cref="Priority"/> must be divisible by 4096 and fit in
/// [0, 61440] — the DB CHECK enforces this.
/// </summary>
public class MstpPriorityRuleStep : EntityBase
{
    public Guid RuleId { get; set; }
    public int StepOrder { get; set; }

    /// <summary>
    /// JSONLogic-ish match expression. Evaluated against a device
    /// context (role / device-type / tags). An empty object matches
    /// every device — useful as the "default" trailing step.
    /// </summary>
    public JsonObject MatchExpression { get; set; } = new();

    public int Priority { get; set; }
}

/// <summary>
/// A concrete MSTP priority assigned to a specific device. Bridge MAC
/// is the natural key on the L2 domain; device ID is the logical key
/// in Central. Both are enforced UNIQUE per tenant (MAC only when
/// known) to prevent two bridges ending up with the same priority.
/// </summary>
public class MstpPriorityAllocation : EntityBase
{
    public Guid RuleId { get; set; }
    public Guid DeviceId { get; set; }
    public string? BridgeMac { get; set; }
    public int Priority { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}
