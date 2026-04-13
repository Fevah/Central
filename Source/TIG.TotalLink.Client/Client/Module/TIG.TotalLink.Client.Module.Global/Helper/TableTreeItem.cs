using DevExpress.Mvvm;
using TIG.TotalLink.Shared.DataModel.Core.Enum;

namespace TIG.TotalLink.Client.Module.Global.Helper
{
    public class TableTreeItem : BindableBase
    {
        #region Private Fields

        private int _id;
        private int? _parentId;
        private string _typeName;
        private string _tableName;
        private string _moduleName;
        private bool _isChecked;
        private bool _isExpanded = true;
        private string _existingFileVersion;
        private DatabaseDomain _databaseDomain;

        #endregion


        #region Public Properties

        /// <summary>
        /// The id of this tree item.
        /// </summary>
        public int Id
        {
            get { return _id; }
            set { SetProperty(ref _id, value, () => Id); }
        }

        /// <summary>
        /// The id of this tree items parent.
        /// </summary>
        public int? ParentId
        {
            get { return _parentId; }
            set { SetProperty(ref _parentId, value, () => ParentId); }
        }

        /// <summary>
        /// The name of the type.
        /// </summary>
        public string TypeName
        {
            get { return _typeName; }
            set
            {
                SetProperty(ref _typeName, value, () => TypeName, () =>
                    {
                        // Abort if the TypeName is empty
                        if (string.IsNullOrWhiteSpace(_typeName))
                            return;

                        // Set the ModuleName to be the second to last part of the TypeName
                        var typeParts = _typeName.Split('.');
                        ModuleName = typeParts[typeParts.Length - 2];
                    });
            }
        }

        /// <summary>
        /// The name of the table.
        /// </summary>
        public string TableName
        {
            get { return _tableName; }
            set { SetProperty(ref _tableName, value, () => TableName); }
        }

        /// <summary>
        /// The name of the module that this table belongs to.
        /// </summary>
        public string ModuleName
        {
            get { return _moduleName; }
            private set { SetProperty(ref _moduleName, value, () => ModuleName); }
        }

        /// <summary>
        /// Indicates if this tree item is checked.
        /// </summary>
        public bool IsChecked
        {
            get { return _isChecked; }
            set { SetProperty(ref _isChecked, value, () => IsChecked); }
        }

        /// <summary>
        /// Indicates if this tree item is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { SetProperty(ref _isExpanded, value, () => IsExpanded); }
        }

        /// <summary>
        /// The version number of any existing export file.
        /// </summary>
        public string ExistingFileVersion
        {
            get { return _existingFileVersion; }
            set { SetProperty(ref _existingFileVersion, value, () => ExistingFileVersion); }
        }

        /// <summary>
        /// The database domain that this table belongs to.
        /// </summary>
        public DatabaseDomain DatabaseDomain
        {
            get { return _databaseDomain; }
            set { SetProperty(ref _databaseDomain, value, () => DatabaseDomain); }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return TableName;
        }

        #endregion
    }
}
