using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
namespace TIG.TotalLink.Shared.DataModel.Repository
{

    public partial class FileGroupVersion
    {
        public FileGroupVersion() : base(Session.DefaultSession) { }
        public FileGroupVersion(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
