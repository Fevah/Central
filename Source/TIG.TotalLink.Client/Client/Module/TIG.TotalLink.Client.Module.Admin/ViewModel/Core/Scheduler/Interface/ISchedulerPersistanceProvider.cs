namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler.Interface
{
    public interface ISchedulerPersistanceProvider
    {
        SchedulerItemViewModel Save(SchedulerItemViewModel obj);
        void Delete(object key);
        string SynchronizerForeignId { get; }
    }
}