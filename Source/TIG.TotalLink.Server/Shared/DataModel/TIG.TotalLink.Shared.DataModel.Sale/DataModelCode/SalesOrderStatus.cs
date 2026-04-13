using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale {

	public partial class SalesOrderStatus {
		public SalesOrderStatus() : base() { }
		public SalesOrderStatus(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
