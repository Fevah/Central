using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Repository {

	public partial class FileExtensionGroup {
		public FileExtensionGroup() : base() { }
		public FileExtensionGroup(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
