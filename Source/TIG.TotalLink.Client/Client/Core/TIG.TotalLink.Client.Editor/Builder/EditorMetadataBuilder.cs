using System;
using System.Collections.Generic;
using System.Linq;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Core.Type;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class EditorMetadataBuilder
    {
        #region Public Methods

        /// <summary>
        /// Builds extended editor metadata.
        /// </summary>
        /// <typeparam name="T">The type that the metadata is for.</typeparam>
        /// <param name="editorWrappers">The list of editor wrappers that will be modified.</param>
        /// <param name="typeWrapper">The type wrapper that will be modified.</param>
        public static void Build<T>(IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper = null)
        {
            Build(typeof(T), editorWrappers, typeWrapper);
        }

        /// <summary>
        /// Builds extended editor metadata.
        /// </summary>
        /// <param name="type">The type that the metadata is for.</param>
        /// <param name="editorWrappers">The list of editor wrappers that will be modified.</param>
        /// <param name="typeWrapper">The type wrapper that will be modified.</param>
        public static void Build(Type type, IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper = null)
        {
            // Call BuildEditorMetadata on the type being modified, and all of its base types
            InvokeBuildEditorMetadata(type, editorWrappers, typeWrapper);

            // If a type wrapper was specified, intialize it
            if (typeWrapper != null)
                typeWrapper.Initialize();
        }

        /// <summary>
        /// Builds extended editor metadata for an instance of an object within a DataLayoutControl.
        /// </summary>
        /// <param name="instance">The object that the metadata is for.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        /// <param name="editorWrappers">The list of editor wrappers that will be modified.</param>
        /// <param name="typeWrapper">The type wrapper that will be modified.</param>
        public static void Build(object instance, DataLayoutControlEx dataLayoutControl, IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper = null)
        {
            var wrapperList = editorWrappers.ToList();

            // Call BuildEditorMetadata on the type being modified, and all of its base types
            InvokeBuildEditorMetadata(instance.GetType(), wrapperList, typeWrapper);

            // Call BuildFormMetadata on the instance
            InvokeBuildFormMetadata(instance, dataLayoutControl, wrapperList, typeWrapper);

            // If a type wrapper was specified, intialize it
            if (typeWrapper != null)
                typeWrapper.Initialize();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Calls BuildEditorMetadata on the specified type and all of its base types.
        /// </summary>
        /// <param name="type">The type to call BuildEditorMetadata on.</param>
        /// <param name="editorWrappers">The list of editor wrappers that will be modified.</param>
        /// <param name="typeWrapper">The type wrapper that will be modified.</param>
        private static void InvokeBuildEditorMetadata(Type type, IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper = null)
        {
            // Build a list containing the target type and all its base types
            var nextType = type;
            var typeList = new List<Type>();
            while (nextType != null)
            {
                typeList.Add(nextType);
                nextType = nextType.BaseType;
            }

            // Invoke BuildEditorMetadata on each type, starting from the most general type
            for (var i = typeList.Count - 1; i > -1; i--)
            {
                nextType = typeList[i];

                // Attempt to get the BuildEditorMetadata method
                var buildEditorMetadataMethod = nextType.GetMethod("BuildEditorMetadata");
                if (buildEditorMetadataMethod != null)
                {
                    // Create a new EditorMetadataBuilder
                    var builderType = typeof(EditorMetadataBuilder<>).MakeGenericType(nextType);
                    var builder = Activator.CreateInstance(builderType, editorWrappers, typeWrapper);

                    // Invoke BuildEditorMetadata on the type
                    buildEditorMetadataMethod.Invoke(null, new[] { builder });
                }
            }
        }

        /// <summary>
        /// Calls BuildFormMetadata on the specified instance.
        /// </summary>
        /// <param name="instance">The object instance to call BuildFormMetadata on.</param>
        /// <param name="editorWrappers">The list of editor wrappers that will be modified.</param>
        /// <param name="dataLayoutControl">The DataLayoutControlEx that is displaying the object.</param>
        /// <param name="typeWrapper">The type wrapper that will be modified.</param>
        private static void InvokeBuildFormMetadata(object instance, DataLayoutControlEx dataLayoutControl, IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper = null)
        {
            // Attempt to get the BuildFormMetadata method
            var type = instance.GetType();
            var buildFormMetadataMethod = type.GetMethod("BuildFormMetadata");
            if (buildFormMetadataMethod != null)
            {
                // Create a new EditorMetadataBuilder
                var builderType = typeof(EditorMetadataBuilder<>).MakeGenericType(type);
                var builder = Activator.CreateInstance(builderType, editorWrappers, typeWrapper);

                // Invoke BuildFormMetadata on the instance
                buildFormMetadataMethod.Invoke(instance, new[] { builder, dataLayoutControl });
            }
        }

        #endregion
    }
}
