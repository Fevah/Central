using MonitoredUndo;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class DataObjectAddChange : DataObjectAddDeleteChangeBase
    {
        #region Constructors

        public DataObjectAddChange(object target, DataObjectBase dataObject)
            : base(target, new ChangeKey<object, DataObjectBase>(target, dataObject))
        {
            DataObject = dataObject;
        }

        #endregion


        #region Overrides

        protected override void PerformUndo()
        {
            DataObject.DeleteDataObject(false);
        }

        protected override void PerformRedo()
        {
            DataObject = DataObject.DuplicateDataObject(false);
        }

        #endregion
    }
}
