using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class CheckboxEditorDefinition : EditorDefinitionBase
    {
        #region Overrides

        public override double DefaultColumnWidth
        {
            get { return 50d; }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
