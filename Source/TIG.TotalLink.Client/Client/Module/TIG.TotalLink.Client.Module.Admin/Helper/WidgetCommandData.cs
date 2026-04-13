using System.Collections.Generic;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetCommandData
    {
        #region Private Fields

        private readonly Dictionary<string, string> _textReplacements = new Dictionary<string, string>();

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of parameter/value pairs that will be replaced in widget command names and descriptions.
        /// </summary>
        public Dictionary<string, string> TextReplacements
        {
            get { return _textReplacements; }
        }

        #endregion
    }
}
