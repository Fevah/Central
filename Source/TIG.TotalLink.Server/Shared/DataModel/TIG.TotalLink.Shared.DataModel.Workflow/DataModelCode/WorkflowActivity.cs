using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Workflow {

	public partial class WorkflowActivity {
		public WorkflowActivity() : base() { }
		public WorkflowActivity(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
