using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class UploadEditorDefinition : EditorDefinitionBase
    {
        private string _fileFilter;

        public string FileFilter
        {
            get { return _fileFilter; }
            set { SetProperty(ref _fileFilter, value, () => FileFilter); }
        }
    }
}