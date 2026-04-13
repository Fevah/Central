using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Document
{
    public class PanelGroupProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<PanelGroup, PanelGroupViewModel>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static PanelGroupViewModel ConvertDataModelToViewModel(PanelGroup dataModel)
        {
            using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
            {
                return scope.Resolve<PanelGroupViewModel>(new TypedParameter(typeof(PanelGroup), dataModel));
            }
        }

        #endregion
    }
}
