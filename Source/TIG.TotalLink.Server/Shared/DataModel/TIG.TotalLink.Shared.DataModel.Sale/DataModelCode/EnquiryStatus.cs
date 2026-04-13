using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Sale
{

    public partial class EnquiryStatus
    {
        public EnquiryStatus() : base() { }
        public EnquiryStatus(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
