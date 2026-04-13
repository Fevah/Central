using System;
using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Admin
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\PostingGroup.xml", dataLayer, s => new PostingGroup(s));

            using (var uow = new UnitOfWork(dataLayer))
            {
                // Create all users in the DefaultUsers list if they don't already exist
                foreach (var defaultUser in DefaultUserCache.DefaultUsers)
                {
                    if (!new XPQuery<User>(uow).Any(u => u.UserName == defaultUser.UserName))
                    {
                        if (defaultUser.IsServiceAccount)
                        {
                            // Create a service account user
                            new User(uow)
                            {
                                Oid = defaultUser.Oid,
                                UserName = defaultUser.UserName,
                                DisplayName = defaultUser.DisplayName,
                                UserType = UserType.System
                            };
                        }
                        else
                        {
                            // Create a TotalLink user
                            var salt = Guid.NewGuid().ToString("N");
                            new User(uow)
                            {
                                UserName = defaultUser.UserName,
                                DisplayName = defaultUser.DisplayName,
                                Password = PasswordHasher.GeneratePasswordHash(defaultUser.EncryptedPassword, salt),
                                Salt = salt,
                                UserType = UserType.TotalLink
                            };
                        }

                        uow.CommitChanges();
                    }
                }

                // If there are no ribbon categories, create the default category
                if (!new XPQuery<RibbonCategory>(uow).Any())
                {
                    new RibbonCategory(uow)
                    {
                        Name = "(Default)",
                        IsDefault = true
                    };
                    uow.CommitChanges();
                }
            }
        }
    }
}
