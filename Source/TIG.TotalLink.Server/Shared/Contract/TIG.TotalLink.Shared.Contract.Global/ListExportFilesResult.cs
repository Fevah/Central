using System.Runtime.Serialization;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Global
{
    [DataContract]
    public class ListExportFilesResult
    {
        #region Public Properties

        /// <summary>
        /// An array of entity changes that occurred.
        /// </summary>
        [DataMember]
        public ExportFileResult[] ExportFiles { get; set; }

        #endregion
    }
}
