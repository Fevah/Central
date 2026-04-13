using TIG.TotalLink.Shared.DataModel.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.Uploader
{
    public class StyleLookupDataModel
    {
        #region Public Properties

        public StyleGender StyleGender { get; set; }

        public StyleCategory StyleCategory { get; set; }

        public ProductCategory ProductCategory { get; set; }

        public ProductType ProductType { get; set; }

        public StyleClass StyleClass { get; set; }

        public Fabric Fabric { get; set; }

        public StyleDepartment StyleDepartment { get; set; }

        public Fit Fit { get; set; }

        public SizeRange SizeRange { get; set; }

        #endregion
    }
}
