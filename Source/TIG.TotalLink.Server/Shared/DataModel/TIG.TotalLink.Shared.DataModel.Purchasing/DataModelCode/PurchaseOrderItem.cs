using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Purchasing {

	public partial class PurchaseOrderItem {
		public PurchaseOrderItem() : base() { }
		public PurchaseOrderItem(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
