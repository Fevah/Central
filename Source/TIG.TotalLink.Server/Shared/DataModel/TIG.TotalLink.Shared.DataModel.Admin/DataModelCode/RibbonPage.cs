using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

	public partial class RibbonPage {
		public RibbonPage() : base() { }
		public RibbonPage(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
