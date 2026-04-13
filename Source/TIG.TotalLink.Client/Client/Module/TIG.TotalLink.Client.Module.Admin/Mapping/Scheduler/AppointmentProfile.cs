using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Scheduler
{
    public class AppointmentProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<Appointment, SchedulerItemViewModel>();
            CreateMap<SchedulerItemViewModel, Appointment>();

            CreateMap<DevExpress.XtraScheduler.Appointment, SchedulerItemViewModel>();
        }

        #endregion

    }
}