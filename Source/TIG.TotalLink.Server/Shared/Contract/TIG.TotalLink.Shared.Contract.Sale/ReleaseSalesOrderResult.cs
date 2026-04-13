using System;
using System.Runtime.Serialization;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    [DataContract]
    public class ReleaseSalesOrderResult
    {
        #region Public Properties

        /// <summary>
        /// The Oid of the SalesOrderRelease that the items were released on.
        /// </summary>
        [DataMember]
        public Guid SalesOrderReleaseOid { get; set; }

        /// <summary>
        /// The Oid of the Delivery that the items were released on.
        /// </summary>
        [DataMember]
        public Guid? DeliveryOid { get; set; }

        /// <summary>
        /// The total number of items that were successfully released.
        /// </summary>
        [DataMember]
        public int TotalQuantityReleased { get; set; }

        /// <summary>
        /// An array of entity changes that occurred.
        /// </summary>
        [DataMember]
        public EntityChange[] Changes { get; set; }

        #endregion
    }
}
