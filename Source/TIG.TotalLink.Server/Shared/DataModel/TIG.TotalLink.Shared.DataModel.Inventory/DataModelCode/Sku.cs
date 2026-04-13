using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{

    public partial class Sku : IReferenceDataObject
    {
        public Sku() : base() { }
        public Sku(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }

        [PersistentAlias("Iif([<DeliveryItem>][Delivery.Status.IsStockAdjusted = 0 AND ^.Oid = Sku].Exists, [<DeliveryItem>][Delivery.Status.IsStockAdjusted = 0 AND ^.Oid = Sku].Sum(Quantity), 0)")]
        public int CommittedStock
        {
            get
            {
                try
                {
                    return (int)(EvaluateAlias("CommittedStock"));
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        [PersistentAlias("PhysicalStock - CommittedStock")]
        public int StockOnHand
        {
            get
            {
                try
                {
                    return (int)(EvaluateAlias("StockOnHand"));
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        [PersistentAlias("Max(StockOnHand, 0)")]
        public int AvailableStock
        {
            get
            {
                try
                {
                    return (int)(EvaluateAlias("AvailableStock"));
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }
    }

}
