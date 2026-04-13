using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Global
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
#if DEBUG
#if TEST
            const string mode = "Dev";
#else
            const string mode = "Debug";
#endif
#else
#if TEST
            const string mode = "Test";
#else
            const string mode = "Release";
#endif
#endif

            DataModelHelper.PopulateTableFromXml(string.Format(@"Data\Setting_{0}.xml", mode), dataLayer, s => new Setting(s));
        }
    }
}
