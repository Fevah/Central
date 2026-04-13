using System.Runtime.Serialization;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Admin
{
    [DataContract]
    public class GetSequenceNumberResult
    {
        #region Public Properties

        /// <summary>
        /// The new Sequence Number that was generated.
        /// </summary>
        [DataMember]
        public long SequenceNumber { get; set; }

        /// <summary>
        /// The Sequence Code from the Sequence that the Sequence Number was generated from.
        /// </summary>
        [DataMember]
        public int SequenceCode { get; set; }

        /// <summary>
        /// An array of entities that were updated.
        /// </summary>
        [DataMember]
        public EntityChange[] EntityChanges { get; set; }

        #endregion
    }
}
