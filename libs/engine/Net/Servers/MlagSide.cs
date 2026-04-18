namespace Central.Engine.Net.Servers;

/// <summary>
/// Which side of an MLAG peer a <see cref="ServerNic"/> lands on.
/// <see cref="None"/> when the NIC isn't MLAG-paired.
///
/// Stored as a single-char column (<c>'A'</c> / <c>'B'</c> / NULL) so
/// the mapping is trivial — see <see cref="ServersRepository"/>'s
/// bind helpers.
/// </summary>
public enum MlagSide
{
    None,
    A,
    B,
}
