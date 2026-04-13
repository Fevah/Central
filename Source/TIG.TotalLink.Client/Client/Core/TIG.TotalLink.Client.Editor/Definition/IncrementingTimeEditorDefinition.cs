using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class IncrementingTimeEditorDefinition : EditorDefinitionBase
    {
        #region Overrides

        public override double DefaultColumnWidth
        {
            get { return 150d; }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
