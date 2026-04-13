using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace TIG.TotalLink.Shared.Contract.Sale
{
    [DataContract]
    public class ReleaseSalesOrderParameters
    {
        #region Constructors

        public ReleaseSalesOrderParameters()
        {
        }

        public ReleaseSalesOrderParameters(Guid salesOrderOid, Guid[] binLocationOids, Guid[] physicalStockTypeOids)
            : this()
        {
            SalesOrderOid = salesOrderOid;
            BinLocationOids = binLocationOids;
            PhysicalStockTypeOids = physicalStockTypeOids;
        }

        public ReleaseSalesOrderParameters(Guid salesOrderOid, Guid[] binLocationOids, Guid[] physicalStockTypeOids, Guid? salesOrderReleaseOid)
            : this(salesOrderOid, binLocationOids, physicalStockTypeOids)
        {
            SalesOrderReleaseOid = salesOrderReleaseOid;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Oid of the SalesOrder to release.
        /// </summary>
        [DataMember]
        public Guid SalesOrderOid { get; set; }

        /// <summary>
        /// The Oid of an existing SalesOrderRelease to append this release to.
        /// If the value is null, a new SalesOrderRelease will be created.
        /// </summary>
        [DataMember]
        public Guid? SalesOrderReleaseOid { get; set; }

        /// <summary>
        /// The Oids of the BinLocations to release stock from.
        /// </summary>
        [DataMember]
        public Guid[] BinLocationOids { get; set; }

        /// <summary>
        /// The Oids of the PhysicalStockTypes to release stock from.
        /// </summary>
        [DataMember]
        public Guid[] PhysicalStockTypeOids { get; set; }

        /// <summary>
        /// A list of the SalesOrderItems to release.
        /// </summary>
        [DataMember]
        public List<SalesOrderItemParameter> SalesOrderItems { get; set; }

        /// <summary>
        /// Indicates if the SalesOrder can be partially delivered.
        /// If this value is null the AllowPartialDelivery flag on the SalesOrder will be used; otherwise this will override the value on the SalesOrder.
        /// </summary>
        [DataMember]
        public bool? AllowPartialDelivery { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a SalesOrderItem to the parameters.
        /// </summary>
        /// <param name="salesOrderItemOid">The Oid of the SalesOrderItem to release.</param>
        /// <param name="quantityToRelease">The quantity of items to release.</param>
        /// <returns>A new SalesOrderItemParameter object.</returns>
        public SalesOrderItemParameter AddSalesOrderItem(Guid salesOrderItemOid, int quantityToRelease)
        {
            // Create the SalesOrderItems list if it hasn't been created already
            if (SalesOrderItems == null)
                SalesOrderItems = new List<SalesOrderItemParameter>();

            // Create a new SalesOrderItemParameter
            var salesOrderItem = new SalesOrderItemParameter()
            {
                SalesOrderItemOid = salesOrderItemOid,
                QuantityToRelease = quantityToRelease
            };

            // Add the SalesOrderItemParameter to the list
            SalesOrderItems.Add(salesOrderItem);

            // Return the SalesOrderItemParameter
            return salesOrderItem;
        }

        #endregion
    }
}
