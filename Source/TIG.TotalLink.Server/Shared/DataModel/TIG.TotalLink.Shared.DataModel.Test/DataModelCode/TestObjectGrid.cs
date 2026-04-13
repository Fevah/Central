using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Test {

	public partial class TestObjectGrid {
		public TestObjectGrid() : base() { }
        public TestObjectGrid(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
