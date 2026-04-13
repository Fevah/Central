using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class SalesOrderRelease_BinLocation
    {
        public SalesOrderRelease_BinLocation() : base(Session.DefaultSession) { }
        public SalesOrderRelease_BinLocation(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
