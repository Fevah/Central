using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

	public partial class User {
		public User() : base() { }
        public User(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
