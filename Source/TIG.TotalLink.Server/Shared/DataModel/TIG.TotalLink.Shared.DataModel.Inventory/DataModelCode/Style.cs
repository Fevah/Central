using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{

    public partial class Style : IReferenceDataObject
    {
        public Style() : base() { }
        public Style(Session session) : base(session) { }
        public override void AfterConstruction() { base.AfterConstruction(); }
    }

}
