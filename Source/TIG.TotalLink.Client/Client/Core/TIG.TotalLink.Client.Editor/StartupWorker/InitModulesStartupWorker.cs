using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.UI;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Descriptor;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.StartupWorker.Core;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition.Interface;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using DataObjectBase = TIG.TotalLink.Shared.DataModel.Core.DataObjectBase;

namespace TIG.TotalLink.Client.Editor.StartupWorker
{
    /// <summary>
    /// Initializes all modules by loading them into an Autofac container and creates AliasedFieldMappings for all necessary data objects.
    /// </summary>
    public class InitModulesStartupWorker : StartupWorkerBase
    {
        #region Private Fields

        private readonly List<string> _excludePatterns;

        #endregion


        #region Constructors

        public InitModulesStartupWorker()
        {
        }

        public InitModulesStartupWorker(params string[] excludePatterns)
            : this()
        {
            _excludePatterns = new List<string>(excludePatterns);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Generates aliased editor wrappers for all aliased fields on data objects in the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to process data objects in.</param>
        /// <returns>A list of EditorWrapperBase objects.</returns>
        private List<EditorWrapperBase> GenerateAliasedWrappers(Assembly assembly)
        {
            var aliasedWrappers = new List<EditorWrapperBase>();

            // Process all types in the assembly which inherit from DataObjectBase or LocalDataObjectBase and are not abtract classes and don't have a NonPersistentAttribute
            foreach (var type in assembly.GetTypes().Where(t => typeof(DataObjectBase).IsAssignableFrom(t) || typeof(LocalDataObjectBase).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttribute<NonPersistentAttribute>(false) == null))
            {
                //System.Diagnostics.Debug.WriteLine("Generating aliases for type {0}", type);

                // If the type inherits from LocalDataObjectBase, add a TypeDescriptionProvider that will provide aliases
                if (typeof(LocalDataObjectBase).IsAssignableFrom(type))
                    TypeDescriptor.AddProvider(new AliasTypeDescriptionProvider(TypeDescriptor.GetProvider(type)), type);

                // Generate aliased wrappers for grid columns
                aliasedWrappers.AddRange(GenerateAliasedWrappers(type, LayoutType.Table));

                // Merge in aliased wrappers for data layout items that haven't already been included
                aliasedWrappers.AddRange(GenerateAliasedWrappers(type, LayoutType.DataForm)
                    .Where(w1 => !aliasedWrappers.Any(w2 => w2.OwnerType == w1.OwnerType && w2.PropertyName == w1.PropertyName))
                );
            }

            return aliasedWrappers;
        }

        /// <summary>
        /// Generates aliased editor wrappers for all aliased fields on the specified type.
        /// </summary>
        /// <param name="type">The type to generate wrappers for.</param>
        /// <param name="layoutType">The type of layout to generate wrappers for.</param>
        /// <returns>A list of EditorWrapperBase objects.</returns>
        private List<EditorWrapperBase> GenerateAliasedWrappers(Type type, LayoutType layoutType)
        {
            var aliasedWrappers = new List<EditorWrapperBase>();

            // Get all visible properties for the type
            var visibleProperties = type.GetVisibleProperties(layoutType);
            if (visibleProperties == null)
                return aliasedWrappers;

            // Create wrappers for each visible property on the type
            var wrappers = visibleProperties.Select(property =>
                    layoutType == LayoutType.Table ? new GridColumnWrapper(type, property) as EditorWrapperBase : new DataLayoutItemWrapper(type, property) as EditorWrapperBase
            ).ToList();

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build(type, wrappers);

            // Store all wrappers which have editors that implement IAliasedEditorDefinition
            aliasedWrappers.AddRange(wrappers.Where(w => w.Editor is IAliasedEditorDefinition && ((IAliasedEditorDefinition)w.Editor).ActualDisplayMember != null && ((IAliasedEditorDefinition)w.Editor).ActualDisplayType != null));

            return aliasedWrappers;
        }

        /// <summary>
        /// Generates AliasedFieldMappings for the supplied GridColumnWrappers.
        /// </summary>
        /// <param name="aliasedColumns">The aliased wrappers to create AliasedFieldMappings for.</param>
        private void GenerateAliasedFieldMappings(List<EditorWrapperBase> aliasedColumns)
        {
            // Process all aliased columns to create AliasedFieldMappings
            foreach (var sourceColumn in aliasedColumns)
            {
                // Add the target field from the initial source column wrapper
                var targetColumn = sourceColumn;
                var targetFieldName = ((IAliasedEditorDefinition)sourceColumn.Editor).ActualDisplayMember;
                var targetFieldType = ((IAliasedEditorDefinition)sourceColumn.Editor).ActualDisplayType;
                var targetFields = new List<AliasTargetField>()
                    {
                        new AliasTargetField(targetFieldName, targetFieldType)
                    };

                // Continue adding target fields until we reach a field that is not DataObjectBase or LocalDataObjectBase
                while (typeof(DataObjectBase).IsAssignableFrom(targetFieldType) || typeof(LocalDataObjectBase).IsAssignableFrom(targetFieldType))
                {
                    // Find the column wrapper for the next target field
                    targetColumn = aliasedColumns.FirstOrDefault(w => w.OwnerType == targetColumn.PropertyType && w.PropertyName == targetFieldName);
                    if (targetColumn == null)
                        break;

                    // Add the target field
                    targetFieldName = ((IAliasedEditorDefinition)targetColumn.Editor).ActualDisplayMember;
                    targetFieldType = ((IAliasedEditorDefinition)targetColumn.Editor).ActualDisplayType;
                    targetFields.Add(new AliasTargetField(targetFieldName, targetFieldType));
                }

                // Create an AliasedFieldMapping containing all target fields found
                DataModelHelper.AliasedFieldMappings.Add(new AliasedFieldMapping(sourceColumn.OwnerType, sourceColumn.DeclaringType, sourceColumn.PropertyName, sourceColumn.PropertyType, targetFields.ToArray()));
            }
        }

        #endregion


        #region Overrides

        public override void Initialize()
        {
            // Record the number of steps it will take to do the work
            Steps = 3;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);

            // Report progress
            ReportProgress(0, "Initializing modules...");

            // Prepare the composition root container
            var builder = new ContainerBuilder();

            // Process all loaded assemblies and register types that implement IModule
            var moduleAssemblies = new List<Assembly>();
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes().Where(t => t != typeof(IModule) && t != typeof(Autofac.Module) && typeof(IModule).IsAssignableFrom(t)))
                    {
                        // Skip this module if its name contains any of the exclude patterns
                        if (_excludePatterns != null && _excludePatterns.Any(p => type.FullName.Contains(p)))
                            continue;

                        // Add the module assembly to the list, if it isn't already there
                        if (!moduleAssemblies.Contains(assembly))
                            moduleAssemblies.Add(assembly);

                        // Create an instance of the module and register it
                        System.Diagnostics.Debug.WriteLine("{0}: Registering module {1}", Assembly.GetEntryAssembly().GetName().Name, type.FullName);
                        var module = (IModule)Activator.CreateInstance(type);
                        builder.RegisterModule(module);
                    }
                }

                // Report progress
                ReportProgress(1, "Initializing editors...");

                // Generate editor wrappers for all aliased fields on data objects in the loaded assemblies
                var aliasedWrappers = new List<EditorWrapperBase>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    aliasedWrappers.AddRange(GenerateAliasedWrappers(assembly));
                }

                // Report progress
                ReportProgress(2, "Initializing aliases...");

                // Generate AliasedFieldMappings for all aliased wrappers
                GenerateAliasedFieldMappings(aliasedWrappers);
            }
            catch (Exception ex)
            {
                throw new Exception("Error during module initialization.", ex);
            }

            // Build the container
            var container = builder.Build();

            // Store the container in the AutofacViewLocator
            AutofacViewLocator.Container = container;

            // Set the default view locator to be an instance of AutofacViewLocator
            // To limit where the view locator needs to look to resolve views, we only supply module assemblies
            // (i.e. only assemblies that register some components with Autofac)
            ViewLocator.Default = new AutofacViewLocator(moduleAssemblies);
        }

        #endregion
    }
}
