using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.RepositoryStore.DataModel {

	public partial class FileData {
		public FileData() : base() { }
		public FileData(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
