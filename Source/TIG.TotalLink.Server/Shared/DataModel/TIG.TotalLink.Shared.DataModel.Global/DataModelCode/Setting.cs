using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Global
{

	public partial class Setting {
		public Setting() : base() { }
		public Setting(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
