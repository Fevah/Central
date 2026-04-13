using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Admin
{

    public partial class DocumentAction
    {
        public DocumentAction() : base(Session.DefaultSession) { }
        public DocumentAction(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
