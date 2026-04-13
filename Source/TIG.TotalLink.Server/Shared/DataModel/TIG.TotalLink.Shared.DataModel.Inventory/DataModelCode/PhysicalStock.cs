using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Inventory {

	public partial class PhysicalStock {
		public PhysicalStock() : base() { }
		public PhysicalStock(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }

        [PersistentAlias("Iif([<PickItem>][DeliveryItem.Delivery.Status.IsStockAdjusted = 0 AND ^.Sku = DeliveryItem.Sku AND ^.BinLocation = BinLocation AND ^.PhysicalStockType = PhysicalStockType].Exists, [<PickItem>][DeliveryItem.Delivery.Status.IsStockAdjusted = 0 AND ^.Sku = DeliveryItem.Sku AND ^.BinLocation = BinLocation AND ^.PhysicalStockType = PhysicalStockType].Sum(Quantity), 0)")]
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
        
        [PersistentAlias("Quantity - CommittedStock")]
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
