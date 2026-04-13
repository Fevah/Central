using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Inventory {

	public partial class Season {
		public Season() : base() { }
		public Season(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
