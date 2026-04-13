using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class FindMethod
    {
        public FindMethod() : base() { }
        public FindMethod(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
