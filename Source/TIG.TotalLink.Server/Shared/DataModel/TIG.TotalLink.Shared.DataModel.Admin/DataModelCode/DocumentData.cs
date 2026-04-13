using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

	public partial class DocumentData {
		public DocumentData() : base() { }
        public DocumentData(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
