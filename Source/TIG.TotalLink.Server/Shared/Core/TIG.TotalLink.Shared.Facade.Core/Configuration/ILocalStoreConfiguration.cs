using System;

namespace TIG.TotalLink.Shared.Facade.Core.Configuration
{
    public interface ILocalStoreConfiguration
    {
        void Load();

        string GetConnection();

        Guid ProviderId { get; }

        string ServerName { get; }

        string DatabaseFile { get; }

        bool UseIntegratedSecurity { get; }

        string UserName { get; }

        string Password { get; }

        bool UseServer { get; }

        string DatabaseName { get; }

    }
}