using Central.Engine.Models;

namespace Central.Engine.Services;

/// <summary>
/// Active Directory integration using System.DirectoryServices.AccountManagement.
/// Browses AD users, imports into Central, syncs active status.
/// </summary>
public static class ActiveDirectoryService
{
    /// <summary>Browse all users in the configured AD domain/OU.</summary>
    public static Task<List<AdUser>> BrowseUsersAsync(AdConfig config, HashSet<string>? importedGuids = null)
    {
        return Task.Run(() =>
        {
            var users = new List<AdUser>();
            if (!config.IsConfigured) return users;

            using var context = CreateContext(config);
            using var searcher = new System.DirectoryServices.AccountManagement.PrincipalSearcher(
                new System.DirectoryServices.AccountManagement.UserPrincipal(context));

            foreach (var result in searcher.FindAll())
            {
                if (result is not System.DirectoryServices.AccountManagement.UserPrincipal up) continue;
                var guid = up.Guid?.ToString() ?? "";
                users.Add(new AdUser
                {
                    ObjectGuid = guid,
                    SamAccountName = up.SamAccountName ?? "",
                    DisplayName = up.DisplayName ?? "",
                    Email = up.EmailAddress ?? "",
                    Phone = up.VoiceTelephoneNumber ?? "",
                    Enabled = up.Enabled ?? false,
                    DistinguishedName = up.DistinguishedName ?? "",
                    LoginName = $"{config.Domain}\\{up.SamAccountName}",
                    IsImported = importedGuids?.Contains(guid) == true
                });
            }
            return users;
        });
    }

    /// <summary>Build sync results: match AD users to existing Central users by ad_guid.</summary>
    public static List<AppUser> BuildSyncUpdates(List<AdUser> adUsers, List<AppUser> existingUsers)
    {
        var updates = new List<AppUser>();
        var byGuid = existingUsers
            .Where(u => !string.IsNullOrEmpty(u.AdGuid))
            .ToDictionary(u => u.AdGuid, u => u);

        foreach (var ad in adUsers)
        {
            if (string.IsNullOrEmpty(ad.ObjectGuid)) continue;
            if (!byGuid.TryGetValue(ad.ObjectGuid, out var user)) continue;

            bool changed = false;
            if (user.DisplayName != ad.DisplayName && !string.IsNullOrEmpty(ad.DisplayName))
            { user.DisplayName = ad.DisplayName; changed = true; }
            if (user.Email != ad.Email && !string.IsNullOrEmpty(ad.Email))
            { user.Email = ad.Email; changed = true; }
            if (user.Phone != ad.Phone && !string.IsNullOrEmpty(ad.Phone))
            { user.Phone = ad.Phone; changed = true; }
            if (user.IsActive != ad.Enabled)
            { user.IsActive = ad.Enabled; changed = true; }

            if (changed)
            {
                user.LastAdSync = DateTime.UtcNow;
                updates.Add(user);
            }
        }
        return updates;
    }

    /// <summary>Convert selected AD users to new AppUser records for import.</summary>
    public static List<AppUser> BuildImportUsers(IEnumerable<AdUser> selected, string defaultRole = "Viewer")
    {
        return selected.Select(ad => new AppUser
        {
            Username = ad.SamAccountName,
            DisplayName = ad.DisplayName,
            Email = ad.Email,
            Phone = ad.Phone,
            AdGuid = ad.ObjectGuid,
            UserType = Auth.UserTypes.ActiveDirectory,
            Role = defaultRole,
            IsActive = ad.Enabled,
            Department = ad.Department,
            Title = ad.Title,
            Company = ad.Company,
        }).ToList();
    }

    private static System.DirectoryServices.AccountManagement.PrincipalContext CreateContext(AdConfig config)
    {
        if (!string.IsNullOrEmpty(config.ServiceAccount) && !string.IsNullOrEmpty(config.ServicePassword))
        {
            return new System.DirectoryServices.AccountManagement.PrincipalContext(
                System.DirectoryServices.AccountManagement.ContextType.Domain,
                config.Domain, config.OuFilter,
                config.ServiceAccount, config.ServicePassword);
        }

        return new System.DirectoryServices.AccountManagement.PrincipalContext(
            System.DirectoryServices.AccountManagement.ContextType.Domain,
            config.Domain, config.OuFilter);
    }
}
