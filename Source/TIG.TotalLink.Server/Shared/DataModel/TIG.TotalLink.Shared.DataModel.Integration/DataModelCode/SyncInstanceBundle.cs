using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Integration {

	public partial class SyncInstanceBundle {
		public SyncInstanceBundle() : base() { }
		public SyncInstanceBundle(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
