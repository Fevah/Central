namespace TIG.TotalLink.Client.Undo.Change
{
    public abstract class DataObjectAddDeleteChangeBase : DataObjectChangeBase
    {
        protected DataObjectAddDeleteChangeBase(object target, object changeKey)
            : base(target, changeKey)
        {
        }

        public override void MergeWith(MonitoredUndo.Change latestChange)
        {
        }
    }
}
