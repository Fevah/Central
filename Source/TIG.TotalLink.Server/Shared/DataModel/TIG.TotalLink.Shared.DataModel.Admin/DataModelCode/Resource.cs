using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin {

	public partial class Resource {
		public Resource() : base() { }
		public Resource(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
