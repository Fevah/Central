using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Repository {

	public partial class File {
		public File() : base() { }
		public File(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
