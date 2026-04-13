using System;
using System.Runtime.Serialization;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    public class PickItemParameter
    {
        #region Public Properties

        /// <summary>
        /// The Oid of the PickItem to release.
        /// </summary>
        [DataMember]
        public Guid PickItemOid { get; set; }

        /// <summary>
        /// The quantity of items to mark as picked.
        /// </summary>
        [DataMember]
        public int QuantityToPick { get; set; }

        #endregion
    }
}
