using MonitoredUndo;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class DataObjectDeleteChange : DataObjectAddDeleteChangeBase
    {
        #region Constructors

        public DataObjectDeleteChange(object target, DataObjectBase dataObject)
            : base(target, new ChangeKey<object, DataObjectBase>(target, dataObject))
        {
            DataObject = dataObject;
        }

        #endregion


        #region Overrides

        public override void MergeWith(MonitoredUndo.Change latestChange)
        {
        }

        protected override void PerformUndo()
        {
            DataObject = DataObject.DuplicateDataObject(false);
        }

        protected override void PerformRedo()
        {
            DataObject.DeleteDataObject(false);
        }

        #endregion
    }
}
