using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Admin.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    public partial class StampedDataObjectBase
    {
        #region Private Methods

        /// <summary>
        /// AfterConstruction method for client/server specific code.
        /// </summary>
        partial void AfterConstructionLocal()
        {
            // If CreatedBy or ModifiedBy are still null, populate them with the current user
            if (CreatedBy == null || ModifiedBy == null)
            {
                var currentUser = GetCurrentUser();

                if (CreatedBy == null)
                    CreatedBy = currentUser;

                if (ModifiedBy == null)
                    ModifiedBy = currentUser;
            }
        }

        /// <summary>
        /// OnSaving method for client/server specific code.
        /// </summary>
        partial void OnSavingLocal()
        {
            // If ModifiedBy hasn't been manually set, populate it with the current user
            if (!_isModifiedBySet)
                ModifiedBy = GetCurrentUser();
        }

        /// <summary>
        /// Gets the default System user.
        /// </summary>
        /// <returns>The default System user.</returns>
        private User GetSystemUser()
        {
            // Get the System user from the DefaultUserCache
            var systemUser = DefaultUserCache.DefaultUsers.FirstOrDefault(u => u.UserName == "system");
            if (systemUser == null)
                return null;

            // Return the corresponding User record from the database
            return Session.GetObjectByKey<User>(systemUser.Oid);
        }

        /// <summary>
        /// Gets the current user for CreatedBy and ModifiedBy stamping.
        /// </summary>
        /// <returns>The current user.</returns>
        private User GetCurrentUser()
        {
            // If a user is being impersonated, return it
            var uow = Session as UnitOfWork;
            if (uow != null)
            {
                var impersonatedUser = uow.GetImpersonatedUser();
                if (impersonatedUser != null)
                    return impersonatedUser;
            }

            // Otherwise, return the system user
            return GetSystemUser();
        }

        #endregion
    }
}
