using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Task {

	public partial class Task {
		public Task() : base() { }
		public Task(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
