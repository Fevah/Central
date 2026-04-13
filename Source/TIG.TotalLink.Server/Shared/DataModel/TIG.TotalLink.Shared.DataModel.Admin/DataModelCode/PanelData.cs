using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

	public partial class PanelData {
		public PanelData() : base() { }
        public PanelData(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
