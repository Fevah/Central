using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Test {

	public partial class TestObject {
		public TestObject() : base() { }
		public TestObject(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
