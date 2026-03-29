namespace Central.Core.Auth;

/// <summary>JIT (just-in-time) user provisioning from external auth providers.</summary>
public interface IUserProvisioningService
{
    /// <summary>Find or create a Central user from an external auth result.</summary>
    Task<AuthUser?> FindOrProvisionUserAsync(AuthenticationResult authResult, string mappedRole);

    /// <summary>Link an external identity to an existing Central user.</summary>
    Task LinkExternalIdentityAsync(int userId, int providerId, string externalId, string? email);
}
