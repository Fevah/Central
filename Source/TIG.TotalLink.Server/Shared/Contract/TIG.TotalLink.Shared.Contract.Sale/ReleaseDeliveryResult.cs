using System;
using System.Runtime.Serialization;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    [DataContract]
    public class ReleaseDeliveryResult
    {
        #region Public Properties

        /// <summary>
        /// The total number of items that were successfully marked as picked.
        /// </summary>
        [DataMember]
        public int TotalQuantityPicked { get; set; }

        /// <summary>
        /// Indicates if the Delivery was marked as dispatched
        /// </summary>
        [DataMember]
        public bool DeliveryDispatched { get; set; }

        /// <summary>
        /// An array of entity changes that occurred.
        /// </summary>
        [DataMember]
        public EntityChange[] Changes { get; set; }

        #endregion
    }
}
