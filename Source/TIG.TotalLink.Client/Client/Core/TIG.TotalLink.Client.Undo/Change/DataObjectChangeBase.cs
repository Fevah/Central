using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Undo.Change
{
    public abstract class DataObjectChangeBase : MonitoredUndo.Change
    {
        #region Constructors

        protected DataObjectChangeBase(object target, object changeKey)
            : base(target, changeKey)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The data object that was modified.
        /// </summary>
        public DataObjectBase DataObject { get; protected set; }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} (DataObject={1})", GetType().Name, DataObject);
        }

        #endregion
    }
}
