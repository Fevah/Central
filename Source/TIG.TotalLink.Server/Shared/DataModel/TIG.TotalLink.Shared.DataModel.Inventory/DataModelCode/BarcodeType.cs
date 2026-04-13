using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Inventory {

	public partial class BarcodeType {
		public BarcodeType() : base() { }
		public BarcodeType(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
