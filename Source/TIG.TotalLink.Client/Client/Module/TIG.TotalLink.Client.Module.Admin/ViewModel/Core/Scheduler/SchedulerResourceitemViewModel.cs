using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler
{
    public class SchedulerResourceitemViewModel : ViewModelBase
    {
        #region Staff

        private string _caption;

        #endregion


        #region Public Properites

        /// <summary>
        /// Caption of Resource
        /// </summary>
        public string Caption
        {
            get { return _caption; }
            set { SetProperty(ref _caption, value, () => Caption); }
        }

        #endregion
    }
}