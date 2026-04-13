using System;
using DevExpress.Xpo;
using DevExpress.Data.Filtering;
using System.Collections.Generic;
using System.ComponentModel;
using TIG.TotalLink.Shared.DataModel.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Sale {

    public partial class Quote : IReferenceDataObject
    {
		public Quote() : base() { }
		public Quote(Session session) : base(session) { }
		public override void AfterConstruction() { base.AfterConstruction(); }
	}

}
