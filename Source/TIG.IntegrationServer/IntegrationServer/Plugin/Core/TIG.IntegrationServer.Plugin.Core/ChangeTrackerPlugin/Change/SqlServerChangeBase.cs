using System;
using System.Data;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Enum;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change
{
    public abstract class SqlServerChangeBase : ChangeBase
    {
        #region Private Properties

        private readonly object _primaryKeyValue;
        private readonly long _changeVersion;
        private readonly string _changeOperation;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructors with data record.
        /// </summary>
        /// <param name="dataRecord">Data record from persistence</param>
        protected SqlServerChangeBase(IDataRecord dataRecord)
        {
            _primaryKeyValue = dataRecord["CHANGETABLE_ID"];
            _changeVersion = (long)dataRecord["SYS_CHANGE_VERSION"];
            _changeOperation = (string)dataRecord["SYS_CHANGE_OPERATION"];
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Change version indicate current entity change version.
        /// </summary>
        public long ChangeVersion
        {
            get { return _changeVersion; }
        }

        /// <summary>
        /// Identity key
        /// </summary>
        public override string Id
        {
            get { return _primaryKeyValue.ToString(); }
        }

        /// <summary>
        /// Change type indicate entity change type.
        /// </summary>
        public override ChangeType Type
        {
            get
            {
                var type = ConvertChangeOperationToChangeType(_changeOperation);
                return type;
            }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Convert change operation to change type.
        /// </summary>
        /// <param name="changeOperationValue">Change operation</param>
        /// <returns>Change type converted from change operation.</returns>
        protected ChangeType ConvertChangeOperationToChangeType(string changeOperationValue)
        {
            switch (changeOperationValue)
            {
                case "I":
                    return ChangeType.Create;
                case "U":
                    return ChangeType.Update;
                case "D":
                    return ChangeType.Delete;
                default:
                    throw new InvalidOperationException(
                        string.Format("Cannot convert \"{0}\" to {1}",
                            changeOperationValue, typeof(ChangeType).FullName));
            }
        }

        #endregion
    }
}
