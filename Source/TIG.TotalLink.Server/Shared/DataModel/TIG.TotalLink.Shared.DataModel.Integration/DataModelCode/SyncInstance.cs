using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Integration {

	public partial class SyncInstance {
		public SyncInstance() : base() { }
		public SyncInstance(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
