using System;
using TIG.IntegrationServer.Logging.Core.LogMessage.Enum;

namespace TIG.IntegrationServer.Logging.Core.LogMessage
{
    [Serializable]
    public class LogMessage
    {
        #region Public Properties

        /// <summary>
        /// Type indicate what kind of type.
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// Which object generate current log.
        /// </summary>
        public Type LogCallerType { get; set; }

        /// <summary>
        /// Log body
        /// </summary>
        public string Body { get; set; }

        #endregion


        #region Overrides

        public override string ToString()
        {
            var str = string.Format("Category={0} LogCaller={1}{2}Body={3}",
                Type,
                LogCallerType,
                Environment.NewLine,
                Body);
            return str;
        }

        #endregion
    }
}
