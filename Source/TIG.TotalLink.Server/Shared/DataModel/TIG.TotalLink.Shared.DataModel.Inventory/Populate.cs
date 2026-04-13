using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Inventory
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\Sequence.xml", dataLayer,
                s => new Sequence(s),
                (s, v) =>
                    s.QueryInTransaction<Sequence>().Any(i => v.ContainsKey("Name") && i.Name == (string)v["Name"])
                );


            DataModelHelper.PopulateTableFromXml(@"Data\UnitOfMeasure.xml", dataLayer, s => new UnitOfMeasure(s));
            DataModelHelper.PopulateTableFromXml(@"Data\PhysicalStockType.xml", dataLayer, s => new PhysicalStockType(s));
            DataModelHelper.PopulateTableFromXml(@"Data\StockAdjustmentReason.xml", dataLayer, s => new StockAdjustmentReason(s));

            using (var uow = new UnitOfWork(dataLayer))
            {
                if (!uow.Query<WarehouseLocation>().Any() && !uow.Query<BinLocation>().Any())
                {
                    var warehouseLocation = new WarehouseLocation(uow)
                    {
                        Name = "Default"
                    };

                    var binLocation = new BinLocation(uow)
                    {
                        Name = "Default",
                        WarehouseLocation = warehouseLocation
                    };
                }

                uow.CommitChanges();
            }
        }
    }
}
