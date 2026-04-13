using System;

namespace TIG.TotalLink.Client.Module.Admin.Enum
{
    [Flags]
    public enum HostTypes
    {
        None = 0,
        Client = 1,
        ServerManager = 2,
        All = Client | ServerManager
    }
}