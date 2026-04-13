using System;
using System.Runtime.Serialization;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    public class SalesOrderItemParameter
    {
        #region Public Properties

        /// <summary>
        /// The Oid of the SalesOrderItem to release.
        /// </summary>
        [DataMember]
        public Guid SalesOrderItemOid { get; set; }

        /// <summary>
        /// The quantity of items to release.
        /// </summary>
        [DataMember]
        public int QuantityToRelease { get; set; }

        #endregion
    }
}
