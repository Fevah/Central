using System;
using System.Reflection;
using System.Windows;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Client.Undo.Helper
{
    public class DataObjectHelper
    {
        /// <summary>
        /// Gets an instance of the facade that manages the specified data object type.
        /// </summary>
        /// <typeparam name="TEntity">The type of data object to get the related facade for.</typeparam>
        /// <returns>The facade that manages this data object type.</returns>
        public static IFacadeBase GetFacade<TEntity>()
            where TEntity : DataObjectBase
        {
            return GetFacade(typeof(TEntity));
        }

        /// <summary>
        /// Gets an instance of the facade that manages the specified data object type.
        /// </summary>
        /// <param name="dataObjectType">The type of data object to get the related facade for.</param>
        /// <returns>The facade that manages this data object type.</returns>
        public static IFacadeBase GetFacade(Type dataObjectType)
        {
            // Attempt to get the FacadeTypeAttribute
            var facadeTypeAttribute = dataObjectType.GetCustomAttribute<FacadeTypeAttribute>(true);
            if (facadeTypeAttribute == null)
                return null;

            // Resolve and return the facade
            return AutofacViewLocator.Default.Resolve(facadeTypeAttribute.FacadeType) as IFacadeBase;
        }

        /// <summary>
        /// Creates a new instance of the specified data object type.
        /// </summary>
        /// <typeparam name="TEntity">The type of data object to create.</typeparam>
        /// <param name="session">The session to create the data object in.</param>
        /// <returns>The new data object.</returns>
        public static TEntity CreateDataObject<TEntity>(Session session)
            where TEntity : DataObjectBase
        {
            return (TEntity)CreateDataObject(typeof(TEntity), session);
        }

        /// <summary>
        /// Creates a new instance of the specified data object type.
        /// </summary>
        /// <param name="dataObjectType">The type of data object to create.</param>
        /// <param name="session">The session to create the data object in.</param>
        /// <returns>The new data object.</returns>
        public static DataObjectBase CreateDataObject(Type dataObjectType, Session session)
        {
            return (DataObjectBase)Activator.CreateInstance(dataObjectType, session);
        }

        /// <summary>
        /// Gets the default dialog window size for the specified data object type.
        /// </summary>
        /// <typeparam name="TEntity">The type of data object to get the dialog size for.</typeparam>
        /// <returns>The default size for the dialog.</returns>
        public static Size GetDefaultDialogSize<TEntity>()
            where TEntity : DataObjectBase
        {
            return GetDefaultDialogSize(typeof(TEntity));
        }

        /// <summary>
        /// Gets the default dialog window size for the specified data object type.
        /// </summary>
        /// <param name="dataObjectType">The type of data object to get the dialog size for.</param>
        /// <returns>The default size for the dialog.</returns>
        public static Size GetDefaultDialogSize(Type dataObjectType)
        {
            // Attempt to get the DialogSizeAttribute
            var dialogSizeAttribute = dataObjectType.GetCustomAttribute<DialogSizeAttribute>(true);

            // If a DialogSizeAttribute return a new Size containing values from the attribute
            if (dialogSizeAttribute != null)
                return new Size(dialogSizeAttribute.DefaultWidth, dialogSizeAttribute.DefaultHeight);

            // Otherwise, return a default Size
            return new Size(500, 600);
        }
    }
}
