using System;
using TIG.IntegrationServer.Security.Cryptography;
using TIG.IntegrationServer.SyncEngine.Core;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context
{
    public interface IContext : IDisposable
    {
        IConfiguration Configuration { get; }
        IPauseDispatcher PauseDispatcher { get; }
        ICancellationDispatcher CancellationDispatcher { get; }
        IHashMaster HashMaster { get; }
        ISyncStatusManager SyncStatusManager { get; }
    }
}
