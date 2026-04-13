using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Test {

	public partial class TestObjectLookUp {
		public TestObjectLookUp() : base() { }
        public TestObjectLookUp(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
