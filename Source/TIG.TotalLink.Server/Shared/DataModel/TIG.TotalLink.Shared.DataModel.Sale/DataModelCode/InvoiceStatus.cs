using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale {

	public partial class InvoiceStatus {
		public InvoiceStatus() : base() { }
		public InvoiceStatus(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
