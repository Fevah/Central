using System.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Repository
{
    public class Populate : IPopulateDataStore
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void PopulateDataStore(IDataLayer dataLayer)
        {
            DataModelHelper.PopulateTableFromXml(@"Data\FileExtension.xml", dataLayer, s => new FileExtension(s));

            using (var uow = new UnitOfWork(dataLayer))
            {
                if (!new XPQuery<FileExtensionGroup>(uow).Any())
                {
                    var fileExtensionGroup = new FileExtensionGroup(uow)
                    {
                        Name = "Document",
                        Description = "Document"
                    };

                    var docEntity = new XPQuery<FileExtension>(uow).FirstOrDefault(p => p.Name == "doc");
                    if (docEntity != null)
                    {
                        fileExtensionGroup.FileExtensions.Add(docEntity);
                    }
                    
                    var docxEntity = new XPQuery<FileExtension>(uow).FirstOrDefault(p => p.Name == "docx");
                    if (docxEntity != null)
                    {
                        fileExtensionGroup.FileExtensions.Add(docxEntity);
                    }

                    var xlsEntity = new XPQuery<FileExtension>(uow).FirstOrDefault(p => p.Name == "xls");
                    if (xlsEntity != null)
                    {
                        fileExtensionGroup.FileExtensions.Add(xlsEntity);
                    }

                    var xlsxEntity = new XPQuery<FileExtension>(uow).FirstOrDefault(p => p.Name == "xlsx");
                    if (xlsxEntity != null)
                    {
                        fileExtensionGroup.FileExtensions.Add(xlsxEntity);
                    }

                    uow.CommitChanges();
                }
            }
        }
    }
}
