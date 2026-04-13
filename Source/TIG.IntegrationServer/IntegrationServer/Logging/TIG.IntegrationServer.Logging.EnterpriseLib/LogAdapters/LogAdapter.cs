using Microsoft.Practices.EnterpriseLibrary.Logging;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Logging.Core.LogMessage;

namespace TIG.IntegrationServer.Logging.EnterpriseLib.LogAdapters
{
    public class LogAdapter : ILog
    {
        #region Private Properties

        private readonly LogWriter _logWriter;
  
        #endregion


        #region Constructors
        
        /// <summary>
        /// Constructor with LogWriter
        /// </summary>
        /// <param name="logWriter">Log Writer</param>
        public LogAdapter(LogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        #endregion


        #region Public Methods
        
        /// <summary>
        /// Message method for write message to log system.
        /// </summary>
        /// <param name="message">Message for record to log system.</param>
        public void Message(LogMessage message)
        {
            _logWriter.Write(message, message.Type.ToString(), -1, 1, message.Type.ToTraceEventType());
        }

        /// <summary>
        /// Dispose log relative objects.
        /// </summary>
        public void Dispose()
        {
            _logWriter.Dispose();
        }

        #endregion
    }
}
