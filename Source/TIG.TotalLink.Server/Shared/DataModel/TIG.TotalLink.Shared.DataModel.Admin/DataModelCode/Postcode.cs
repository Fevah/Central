using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin {

	public partial class Postcode {
		public Postcode() : base() { }
		public Postcode(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
