using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Test
{
    public class TestViewModel : LocalDataObjectBase
    {
        #region Private Fields

        private string _name;

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the test item.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return Name;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<TestViewModel> builder)
        {
            builder.TableLayout().Group("")
                .ContainsProperty(p => p.Name);

            builder.DataFormLayout().TabbedGroup("Tabs")
                .Group("General")
                    .ContainsProperty(p => p.Name);
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<TestViewModel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.Name);
        }

        #endregion
    }
}
