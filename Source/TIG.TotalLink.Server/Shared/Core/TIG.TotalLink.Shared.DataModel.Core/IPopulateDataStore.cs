using DevExpress.Xpo;

namespace TIG.TotalLink.Shared.DataModel.Core
{
    public interface IPopulateDataStore
    {
        /// <summary>
        /// Populates entities using the supplied data layer.
        /// </summary>
        /// <param name="dataLayer">A data layer that can be used to access the data store.</param>
        void PopulateDataStore(IDataLayer dataLayer);
    }
}
