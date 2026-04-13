using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Repository
{

    public partial class FileBinderVersion
    {
        public FileBinderVersion() : base(Session.DefaultSession) { }
        public FileBinderVersion(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
