using System.Runtime.Serialization;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Global
{
    [DataContract]
    public class ExportFileResult
    {
        #region Public Properties

        /// <summary>
        /// The name of the table.
        /// </summary>
        [DataMember]
        public string TableName { get; set; }

        /// <summary>
        /// The version of the data in the export file.
        /// </summary>
        [DataMember]
        public string Version { get; set; }

        #endregion
    }
}
