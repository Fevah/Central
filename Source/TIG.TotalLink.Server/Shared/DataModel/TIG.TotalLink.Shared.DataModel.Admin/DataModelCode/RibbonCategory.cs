using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

	public partial class RibbonCategory {
		public RibbonCategory() : base() { }
        public RibbonCategory(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
