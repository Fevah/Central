using Central.Core.Auth;
using Central.Core.Models;
using Central.Data;
using Central.Data.Repositories;

namespace Central.Desktop.Auth;

/// <summary>
/// JIT (just-in-time) user provisioning from external auth providers.
/// Creates Central users on first login, links external identities.
/// </summary>
public class UserProvisioningService : IUserProvisioningService
{
    private readonly DbRepository _repo;
    private readonly PermissionRepository _permRepo;

    public UserProvisioningService(string dsn)
    {
        _repo = new DbRepository(dsn);
        _permRepo = new PermissionRepository(dsn);
    }

    public async Task<AuthUser?> FindOrProvisionUserAsync(AuthenticationResult authResult, string mappedRole)
    {
        // 1. Check by external identity link
        if (authResult.ProviderId.HasValue && !string.IsNullOrEmpty(authResult.ExternalId))
        {
            var identities = await _repo.GetUserExternalIdentitiesAsync();
            var linked = identities.FirstOrDefault(i =>
                i.ProviderId == authResult.ProviderId.Value &&
                i.ExternalId == authResult.ExternalId);
            if (linked != null)
            {
                var user = await _permRepo.GetUserByIdAsync(linked.UserId);
                if (user != null) return user;
            }
        }

        // 2. Check by username match
        var username = authResult.Username ?? authResult.Email?.Split('@')[0] ?? "";
        if (!string.IsNullOrEmpty(username))
        {
            var existing = await _permRepo.GetUserByUsernameAsync(username);
            if (existing != null)
            {
                // Link external identity if not already linked
                if (authResult.ProviderId.HasValue && !string.IsNullOrEmpty(authResult.ExternalId))
                    await _repo.LinkExternalIdentityAsync(existing.Id, authResult.ProviderId.Value,
                        authResult.ExternalId, authResult.Email);
                return existing;
            }
        }

        // 3. Check by email match
        if (!string.IsNullOrEmpty(authResult.Email))
        {
            var byEmail = await _permRepo.GetUserByEmailAsync(authResult.Email);
            if (byEmail != null)
            {
                if (authResult.ProviderId.HasValue && !string.IsNullOrEmpty(authResult.ExternalId))
                    await _repo.LinkExternalIdentityAsync(byEmail.Id, authResult.ProviderId.Value,
                        authResult.ExternalId, authResult.Email);
                return byEmail;
            }
        }

        // 4. JIT provision — create new user
        var newUser = new AppUser
        {
            Username = username,
            DisplayName = authResult.DisplayName ?? username,
            Email = authResult.Email ?? "",
            Role = mappedRole,
            IsActive = true,
            UserType = authResult.ProviderType switch
            {
                "entra_id" => UserTypes.ActiveDirectory,
                "okta" => "Okta",
                "saml2" => "Saml",
                _ => UserTypes.Standard
            }
        };

        await _repo.UpsertUserAsync(newUser);

        // Link external identity
        if (authResult.ProviderId.HasValue && !string.IsNullOrEmpty(authResult.ExternalId))
            await _repo.LinkExternalIdentityAsync(newUser.Id, authResult.ProviderId.Value,
                authResult.ExternalId, authResult.Email);

        // Seed default notification preferences for the new user
        if (newUser.Id > 0)
        {
            try
            {
                foreach (var eventType in Core.Models.NotificationEventTypes.All)
                    await _repo.UpsertNotificationPreferenceAsync(newUser.Id, eventType, "toast", true);
            }
            catch { /* non-critical */ }
        }

        // Return as AuthUser
        return await _permRepo.GetUserByUsernameAsync(newUser.Username);
    }

    public async Task LinkExternalIdentityAsync(int userId, int providerId, string externalId, string? email)
    {
        await _repo.LinkExternalIdentityAsync(userId, providerId, externalId, email);
    }
}
