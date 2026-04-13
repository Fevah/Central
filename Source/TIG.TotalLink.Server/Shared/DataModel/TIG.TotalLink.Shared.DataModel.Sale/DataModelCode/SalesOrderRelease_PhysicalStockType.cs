using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class SalesOrderRelease_PhysicalStockType
    {
        public SalesOrderRelease_PhysicalStockType() : base(Session.DefaultSession) { }
        public SalesOrderRelease_PhysicalStockType(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
