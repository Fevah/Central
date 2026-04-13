using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Crm {

	public partial class Language {
		public Language() : base() { }
		public Language(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
