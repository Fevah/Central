using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class SalesOrderReleaseItem
    {
        public SalesOrderReleaseItem() : base(Session.DefaultSession) { }
        public SalesOrderReleaseItem(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
