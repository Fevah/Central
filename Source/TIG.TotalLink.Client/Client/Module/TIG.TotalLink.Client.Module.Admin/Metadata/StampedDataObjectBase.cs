using System;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    [FacadeType(typeof(IAdminFacade))]
    public partial class StampedDataObjectBase
    {
        #region Private Methods

        /// <summary>
        /// AfterConstruction method for client/server specific code.
        /// </summary>
        partial void AfterConstructionLocal()
        {
            // If CreatedBy or ModifiedBy are still null, populate them with the authenticated user
            if (CreatedBy == null || ModifiedBy == null)
            {
                var user = GetAuthenticatedUser();

                if (CreatedBy == null)
                    CreatedBy = user;

                if (ModifiedBy == null)
                    ModifiedBy = user;
            }
        }

        /// <summary>
        /// OnSaving method for client/server specific code.
        /// </summary>
        partial void OnSavingLocal()
        {
            // If ModifiedBy hasn't been manually set, populate it with the authenticated user
            if (!_isModifiedBySet)
                ModifiedBy = GetAuthenticatedUser();
        }

        /// <summary>
        /// Gets the authenticated user record.
        /// </summary>
        /// <returns>The authenticated user.</returns>
        private User GetAuthenticatedUser()
        {
            // Abort if there is no authenticated user
            if (AppContextViewModel.Instance.UserInfo == null)
                return null;

            // Get the authenticated user
            User authenticatedUser;
            try
            {
                authenticatedUser = Session.GetObjectByKey<User>(AppContextViewModel.Instance.UserInfo.Oid);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to get authenticated user!", ex);
            }

            // Return the authenticated user
            return authenticatedUser;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<StampedDataObjectBase> builder)
        {
            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("Tracking")
                    .ContainsProperty(p => p.CreatedBy)
                    .ContainsProperty(p => p.CreatedDate)
                    .ContainsProperty(p => p.ModifiedBy)
                    .ContainsProperty(p => p.ModifiedDate);

            builder.Property(p => p.CreatedBy).ReadOnly();
            builder.Property(p => p.CreatedDate).ReadOnly();
            builder.Property(p => p.ModifiedBy).ReadOnly();
            builder.Property(p => p.ModifiedDate).ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<StampedDataObjectBase> builder)
        {
            builder.GridBaseColumnEditors()
                .Property(p => p.CreatedBy).Hidden().EndProperty()
                .Property(p => p.CreatedDate).Hidden().EndProperty()
                .Property(p => p.ModifiedBy).Hidden().EndProperty()
                .Property(p => p.ModifiedDate).Hidden().EndProperty();

            builder.Property(p => p.CreatedDate).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
            builder.Property(p => p.ModifiedDate).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
        }

        #endregion
    }
}
