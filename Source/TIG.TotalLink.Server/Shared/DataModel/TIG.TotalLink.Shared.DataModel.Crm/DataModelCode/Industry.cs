using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Crm {

	public partial class Industry {
		public Industry() : base() { }
		public Industry(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
