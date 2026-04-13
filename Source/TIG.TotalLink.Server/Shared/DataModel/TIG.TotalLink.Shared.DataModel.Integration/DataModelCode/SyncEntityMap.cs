using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Integration {

	public partial class SyncEntityMap {
		public SyncEntityMap() : base() { }
		public SyncEntityMap(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
