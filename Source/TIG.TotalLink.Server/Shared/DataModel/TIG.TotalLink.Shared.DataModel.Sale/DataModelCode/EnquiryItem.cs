using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class EnquiryItem
    {
        public EnquiryItem() : base() { }
        public EnquiryItem(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
