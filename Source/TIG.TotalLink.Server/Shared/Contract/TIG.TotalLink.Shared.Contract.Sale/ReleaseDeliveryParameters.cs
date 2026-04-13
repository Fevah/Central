using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    [DataContract]
    public class ReleaseDeliveryParameters
    {
        #region Constructors

        public ReleaseDeliveryParameters()
        {
        }

        public ReleaseDeliveryParameters(Guid deliveryOid)
            : this()
        {
            DeliveryOid = deliveryOid;
        }

        public ReleaseDeliveryParameters(Guid deliveryOid, string consignmentNote)
            : this()
        {
            DeliveryOid = deliveryOid;

            if (!string.IsNullOrWhiteSpace(consignmentNote))
                ConsignmentNote = consignmentNote;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Oid of the Delivery to release.
        /// </summary>
        [DataMember]
        public Guid DeliveryOid { get; set; }

        /// <summary>
        /// The ConsignmentNote to apply to the delivery.
        /// Setting this will mark the delivery as dispatched and any remaining items will be considered short-shipped.
        /// </summary>
        [DataMember]
        public string ConsignmentNote { get; set; }

        /// <summary>
        /// A list of the PickItems to release.
        /// </summary>
        [DataMember]
        public List<PickItemParameter> PickItems { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a PickItem to the parameters.
        /// </summary>
        /// <param name="pickItemOid">The Oid of the PickItem to release.</param>
        /// <param name="quantityToPick">The quantity of items to mark as picked.</param>
        /// <returns>A new PickItemParameter object.</returns>
        public PickItemParameter AddPickItem(Guid pickItemOid, int quantityToPick)
        {
            // Create the PickItems list if it hasn't been created already
            if (PickItems == null)
                PickItems = new List<PickItemParameter>();

            // Create a new PickItemParameter
            var pickItem = new PickItemParameter()
            {
                PickItemOid = pickItemOid,
                QuantityToPick = quantityToPick
            };

            // Add the PickItemParameter to the list
            PickItems.Add(pickItem);

            // Return the PickItemParameter
            return pickItem;
        }

        #endregion
    }
}
