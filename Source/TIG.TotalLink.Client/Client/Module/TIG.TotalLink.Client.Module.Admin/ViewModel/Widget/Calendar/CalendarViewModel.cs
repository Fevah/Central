using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AutoMapper;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Calendar
{
    public class CalendarViewModel : SchedulerViewModelBase<Appointment>
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with test facade.
        /// </summary>
        /// <param name="adminFacade">Test facade for invoke service.</param>
        public CalendarViewModel(IAdminFacade adminFacade)
            : base(adminFacade)
        {
            // Store services.
            _adminFacade = adminFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the AdminFacade
                ConnectToFacade(_adminFacade);

                var appointments = _adminFacade.ExecuteQuery(uow => uow.Query<Appointment>());
                ItemsSource = Mapper.Map<IEnumerable<Appointment>, ObservableCollection<SchedulerItemViewModel>>(appointments);
            });
        }

        public override string SynchronizerForeignId
        {
            get { return "OutlookEntryId"; }
        }

        #endregion
    }
}