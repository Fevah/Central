namespace Central.Engine.Net;

/// <summary>
/// Lock level on every entity in the networking engine.
/// Mirrors <c>net.lock_state</c> in PostgreSQL.
/// </summary>
public enum LockState
{
    /// <summary>Any authorised user can edit.</summary>
    Open,

    /// <summary>Edit allowed with warning + reason captured.</summary>
    SoftLock,

    /// <summary>Edit requires a Change Set + N approvers (configurable per entity).</summary>
    HardLock,

    /// <summary>Never editable. Only replace via decommission + recreate.</summary>
    Immutable,
}
