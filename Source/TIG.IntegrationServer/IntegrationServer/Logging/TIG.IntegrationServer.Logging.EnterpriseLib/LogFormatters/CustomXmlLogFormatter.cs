using System.Xml.Linq;
using Microsoft.Practices.EnterpriseLibrary.Logging;
using Microsoft.Practices.EnterpriseLibrary.Logging.Formatters;

namespace TIG.IntegrationServer.Logging.EnterpriseLib.LogFormatters
{
    public class CustomXmlLogFormatter : XmlLogFormatter
    {
        #region Override

        /// <summary>
        /// Override parse to xml format.
        /// </summary>
        /// <param name="log">Log entry</param>
        /// <returns>Return xml content</returns>
        public override string Format(LogEntry log)
        {
            var originalString = base.Format(log);
            var xe = XElement.Parse(originalString);
            var customString = xe.ToString(SaveOptions.None);
            return customString;
        }

        #endregion
    }
}
