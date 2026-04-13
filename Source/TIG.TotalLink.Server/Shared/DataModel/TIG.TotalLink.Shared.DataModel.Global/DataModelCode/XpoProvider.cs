using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Global
{

	public partial class XpoProvider {
		public XpoProvider() : base() { }
		public XpoProvider(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
