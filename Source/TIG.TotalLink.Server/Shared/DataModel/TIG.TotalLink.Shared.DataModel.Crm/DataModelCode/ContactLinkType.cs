using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Crm {

    public partial class ContactLinkType
    {
        public ContactLinkType() : base() { }
		public ContactLinkType(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
