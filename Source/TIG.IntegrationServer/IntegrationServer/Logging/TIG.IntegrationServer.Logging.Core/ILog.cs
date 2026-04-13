using System;

namespace TIG.IntegrationServer.Logging.Core
{
    public interface ILog : IDisposable
    {
        void Message(LogMessage.LogMessage msg);
    }
}
