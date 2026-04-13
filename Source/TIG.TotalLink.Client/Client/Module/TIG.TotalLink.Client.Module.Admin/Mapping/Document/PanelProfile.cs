using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Document
{
    public class PanelProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<Panel, PanelViewModel>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static PanelViewModel ConvertDataModelToViewModel(Panel dataModel)
        {
            using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
            {
                return scope.Resolve<PanelViewModel>(new TypedParameter(typeof(Panel), dataModel));
            }
        }

        #endregion
    }
}
