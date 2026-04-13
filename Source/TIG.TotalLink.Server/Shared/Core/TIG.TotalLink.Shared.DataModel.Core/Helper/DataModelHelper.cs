using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using DevExpress.Data.Async.Helpers;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Extension;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class DataModelHelper
    {
        #region Private Fields

        private static readonly List<AliasedFieldMapping> _aliasedFieldMappings = new List<AliasedFieldMapping>();

        #endregion


        #region Public Fields

        public static readonly List<string> NonSerializableDataObjectBaseProperties = new List<string>() { "This", "Loading", "ClassInfo", "Session", "IsLoading", "IsDeleted", "IsLocalOnly", "AliasValues" };

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of AliasedFieldMappings for datamodels in all loaded assemblies.
        /// </summary>
        public static List<AliasedFieldMapping> AliasedFieldMappings
        {
            get { return _aliasedFieldMappings; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a ReflectionDictionary containing information about entities in the specified assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to collect entity information from.</param>
        /// <returns>A ReflectionDictionary containing information about entities in the specified assemblies.</returns>
        public static ReflectionDictionary GetReflectionDictionary(params Assembly[] assemblies)
        {
            // Create a dictionary that contains all XPO types that exist in the specified assemblies
            var dict = new ReflectionDictionary();
            dict.GetDataStoreSchema(assemblies);

            // Process all classes in the ReflectionDictionary...
            foreach (var classInfo in dict.Classes.Cast<XPClassInfo>())
            {
                if (classInfo.IsPersistent)
                {
                    // Create associations for all members on this class which have a RuntimeAssociationAttribute
                    CreateRuntimeAssociations(classInfo);
                }

                // Create dynamic persistent aliases to handle sorting/grouping/filtering for each AliasedFieldMapping that exists for this class type
                CreateDynamicAliases(classInfo);
            }

            // Return the ReflectionDictionary
            return dict;
        }

        /// <summary>
        /// Extracts the contained DataObjectBase from any wrappers it may be encapsulated within.
        /// </summary>
        /// <param name="obj">The object to extract the DataObjectBase from.</param>
        /// <returns>The extracted DataObjectBase.</returns>
        public static DataObjectBase GetDataObject(object obj)
        {
            // Check if the supplied object is a ReadonlyThreadSafeProxyForObjectFromAnotherThread and extract the data object from it
            var proxy = obj as ReadonlyThreadSafeProxyForObjectFromAnotherThread;
            if (proxy != null)
                return proxy.OriginalRow as DataObjectBase;

            // Return the original object cast to a DataObjectBase
            return obj as DataObjectBase;
        }

        /// <summary>
        /// Populates a table with new rows from the specified file.
        /// No changes will be made if the table already contains any rows.
        /// </summary>
        /// <typeparam name="T">The type of entity to create.</typeparam>
        /// <param name="path">
        /// The path to an xml file that contains data about the entities to create.
        /// This file must be included in the project with the Build Action set to Embedded Resource.
        /// </param>
        /// <param name="dataLayer">The data layer to create items with.</param>
        /// <param name="createFunc">A function which can create an object of type <typeparamref name="T"/>.</param>
        /// <param name="existsFunc">
        /// If this function is included, it will be executed for each row to determine if it already exists, and a new row will only be created if the function returns false.
        /// If this function is excluded, new rows will only be created if the table is empty.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void PopulateTableFromXml<T>(string path, IDataLayer dataLayer, Func<Session, T> createFunc, Func<Session, Dictionary<string, object>, bool> existsFunc = null)
            where T : DataObjectBase
        {
            // Start a new UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Abort if the existsFunc is null and the table already contains some rows
                if (existsFunc == null && uow.Query<T>().Any())
                    return;

                // Attempt to get a stream containing the xml file
                var entityType = typeof(T);
                var assembly = Assembly.GetCallingAssembly();
                var resourceName = string.Format("{0}.{1}", assembly.GetName().Name, path.Replace("\\", "."));
                var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new Exception(string.Format("Error populating {0}!\r\nFailed to load resource from \"{1}\".",
                        entityType.Name, path));

                // Read the xml and remove comments
                string xml;
                using (var sr = new StreamReader(stream))
                {
                    xml = Regex.Replace(sr.ReadToEnd(), "<!--.*?-->", "");
                }

                // Load the xml into an XmlDocument
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                // Attempt to get the root node
                var rootElement = doc.DocumentElement;
                if (rootElement == null)
                    throw new Exception(string.Format(
                        "Error populating {0}!\r\nFailed to find root element in \"{1}\".", entityType.Name, path));

                // Process each child element (row)
                foreach (var childElement in rootElement.ChildNodes.OfType<XmlElement>())
                {
                    var values = new Dictionary<string, object>();

                    // Collect each attribute value for the corresponding property
                    foreach (var propertyAttribute in childElement.Attributes.OfType<XmlAttribute>())
                    {
                        values.Add(propertyAttribute.Name, ProcessPropertyValue(uow, path, entityType, propertyAttribute.Name, propertyAttribute.Value));
                    }

                    // Collect each child element value for the corresponding property
                    foreach (var propertyElement in childElement.ChildNodes.OfType<XmlElement>())
                    {
                        values.Add(propertyElement.Name, ProcessPropertyValue(uow, path, entityType, propertyElement.Name, propertyElement.InnerXml));
                    }

                    // Execute the existsFunc and abort if the item already exists
                    if (existsFunc != null && existsFunc(uow, values))
                        continue;

                    // Create a new item and apply the property values
                    var item = createFunc(uow);
                    foreach (var kvp in values)
                    {
                        var property = entityType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                        if (property == null)
                            continue;

                        property.SetValue(item, kvp.Value);
                    }
                }

                // Commit all changes
                uow.CommitChanges();
            }
        }

        /// <summary>
        /// Read file content from specified file.
        /// </summary>
        /// <param name="path">
        /// The path to an xml file that contains data about the fields mapping information.
        /// This file must be included in the project with the Build Action set to Embedded Resource.
        /// </param>
        /// <returns>Resource content</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string ReadResourceContent(string path)
        {
            var assembly = Assembly.GetCallingAssembly();
            var resourceName = string.Format("{0}.{1}", assembly.GetName().Name, path.Replace("\\", "."));
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new Exception(string.Format("Failed to load resource from \"{0}\".", path));

            // Reader content from stream.
            var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Attempts to find a type in all loaded assemblies.
        /// </summary>
        /// <param name="typeName">The name of the type to find.</param>
        /// <returns>The type if one was found; otherwise null.</returns>
        public static Type GetTypeFromLoadedAssemblies(string typeName)
        {
            // Attempt to find the type in the current assembly
            var type = Type.GetType(typeName);
            if (type != null)
                return type;

            // Iterate all loaded assemblies and try to find the type in each
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(typeName);
                if (type != null)
                    return type;
            }

            // If no type was found anywhere, return null
            return null;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Collects, converts and returns a property value.
        /// </summary>
        /// <param name="uow">The UnitOfWork for modifying the item.</param>
        /// <param name="path">The path of the source file.</param>
        /// <param name="entityType">The type of item the value will be stored on.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="propertyValue">The value of the property.</param>
        private static object ProcessPropertyValue(UnitOfWork uow, string path, Type entityType, string propertyName, string propertyValue)
        {
            if (propertyValue != null)
                propertyValue = propertyValue.Trim();

            // Attempt to get the target property
            var property = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                throw new Exception(string.Format("Error populating \"{0}\" in \"{1}\"!\r\nUnknown property \"{2}\".", entityType.Name, path, propertyName));

            // If the string value is a query for another object, attempt to parse and process it
            if (propertyValue != null
                && ((propertyValue.StartsWith("<Query") && propertyValue.EndsWith("/>"))
                || (propertyValue.StartsWith("<Query>") && propertyValue.EndsWith("</Query>"))))
            {
                try
                {
                    propertyValue = ProcessValueQuery(uow, propertyValue);
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Error processing query for \"{0}.{1}\" in \"{2}\"!\r\n{3}", entityType.Name, propertyName, path, ex.Message), ex);
                }
            }

            // Convert and apply the value
            object value = null;
            TypeSwitch.On(property.PropertyType)
                .Case<string>(() => { value = propertyValue; })
                .Case<int>(() =>
                {
                    int i;
                    value = (int.TryParse(propertyValue, out i) ? i : default(int));
                })
                .Case<int?>(() =>
                {
                    int i;
                    value = (int.TryParse(propertyValue, out i) ? (int?)i : default(int?));
                })
                .Case<long>(() =>
                {
                    long l;
                    value = (long.TryParse(propertyValue, out l) ? l : default(long));
                })
                .Case<long?>(() =>
                {
                    long l;
                    value = (long.TryParse(propertyValue, out l) ? (long?)l : default(long?));
                })
                .Case<bool>(() =>
                {
                    bool b;
                    value = (bool.TryParse(propertyValue, out b) ? b : default(bool));
                })
                .Case<bool?>(() =>
                {
                    bool b;
                    value = (bool.TryParse(propertyValue, out b) ? (bool?)b : default(bool?));
                })
                .Case<decimal>(() =>
                {
                    decimal d;
                    value = (decimal.TryParse(propertyValue, out d) ? d : default(decimal));
                })
                .Case<decimal?>(() =>
                {
                    decimal d;
                    value = (decimal.TryParse(propertyValue, out d) ? (decimal?)d : default(decimal?));
                })
                .Case<Guid>(() =>
                {
                    Guid g;
                    value = (Guid.TryParse(propertyValue, out g) ? g : default(Guid));
                });

            return value;
        }

        /// <summary>
        /// Processes a query value and returns the result.
        /// </summary>
        /// <param name="uow">The UnitOfWork for executing the query.</param>
        /// <param name="query">An xml fragment describing the query to execute.</param>
        /// <returns>The result of the processed query.</returns>
        private static string ProcessValueQuery(UnitOfWork uow, string query)
        {
            // Load the query into an XmlDocument
            var doc = new XmlDocument();
            doc.LoadXml(query);

            // Attempt to get the first child of the document as an XmlElement
            var queryElement = doc.FirstChild as XmlElement;
            if (queryElement == null || queryElement.Name != "Query")
                throw new Exception("Failed to find Query element.");

            // Collect attribute values from the element
            var typeString = GetAttributeValue(queryElement, "Type");
            var criteriaString = GetAttributeValue(queryElement, "Criteria");
            var propertyString = GetAttributeValue(queryElement, "Property");

            // Attempt to convert the typeString to a Type
            var sourceType = GetTypeFromLoadedAssemblies(typeString);
            if (sourceType == null)
                throw new Exception(string.Format("Failed to find type \"{0}\".", typeString));

            // Attempt to get the XPClassInfo for the type
            var sourceClassInfo = uow.GetClassInfo(sourceType);
            if (sourceClassInfo == null)
                throw new Exception(string.Format("Failed to find XPClassInfo for type \"{0}\".", typeString));

            // Attempt to get the source property
            var sourceProperty = sourceType.GetProperty(propertyString, BindingFlags.Public | BindingFlags.Instance);
            if (sourceProperty == null)
                throw new Exception(string.Format("Unknown property \"{0}\" on type \"{1}\".", propertyString, typeString));

            // Attempt to parse the criteria
            CriteriaOperator criteria;
            try
            {
                criteria = CriteriaOperator.Parse(criteriaString);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to parse criteria for property \"{0}\".\r\n{1}", propertyString, ex.Message), ex);
            }

            // Execute the query and collect the first object found
            var foundObject = uow.GetObjects(sourceClassInfo, criteria, null, 1, false, false).Cast<object>().FirstOrDefault();

            // Abort if no object was found
            if (foundObject == null)
                return null;

            // Get the property value from the found object
            var value = sourceProperty.GetValue(foundObject);

            // Convert the property value to a string
            string stringValue = null;
            if (value != null)
                stringValue = value.ToString();

            // Return the string value
            return stringValue;
        }

        /// <summary>
        /// Collects the value of an attribute from an XmlElement.
        /// </summary>
        /// <param name="element">The element to collect the attribute from.</param>
        /// <param name="attributeName">The name of the attribute to collect.</param>
        /// <returns>The value of the attribute.</returns>
        private static string GetAttributeValue(XmlElement element, string attributeName)
        {
            var attribute = element.Attributes[attributeName];
            if (attribute == null)
                throw new Exception(string.Format("{0} is missing the {1} attribute.", element.Name, attributeName));

            return attribute.Value != null ? attribute.Value.Trim() : null;
        }

        /// <summary>
        /// Creates associations for all members on classInfo which are owned by owner.
        /// </summary>
        /// <param name="classInfo">The class to create associations on.</param>
        /// <param name="owner">The class that owns the members to create associations on.  If null, only members defined on classInfo will be processed.</param>
        private static void CreateRuntimeAssociations(XPClassInfo classInfo, XPClassInfo owner = null)
        {
            // If owner is not specified, only process members that belong to classInfo
            if (owner == null)
                owner = classInfo;

            // Process all properties which reference object types and are defined in the owner class...
            foreach (var memberInfo in classInfo.ObjectProperties.Cast<XPMemberInfo>().Where(m => m.Owner == owner))
            {
                // If the property has a RuntimeAssociationAttribute, we will create an association from this data model to the related external data model
                var runtimeAssociationAttribute = memberInfo.FindAttributeInfo(typeof(RuntimeAssociationAttribute)) as RuntimeAssociationAttribute;
                if (runtimeAssociationAttribute != null)
                {
                    // Create a collection property and association on the class this property refers to
                    var relatedClassInfo = classInfo.Dictionary.GetClassInfo(memberInfo.MemberType);
                    var relatedCollectionType = typeof(XPCollection<>).MakeGenericType(classInfo.ClassType);
                    relatedClassInfo.CreateMember(runtimeAssociationAttribute.CollectionName, relatedCollectionType, true, false, new AssociationAttribute(runtimeAssociationAttribute.AssociationName, classInfo.ClassType));

                    // Add an association to the source property
                    memberInfo.AddAttribute(new AssociationAttribute(runtimeAssociationAttribute.AssociationName));
                }
            }
        }

        /// <summary>
        /// Create dynamic persistent aliases to handle sorting/grouping/filtering for each AliasedFieldMapping that exists for the classInfo.
        /// </summary>
        /// <param name="classInfo">The class to create dynamic aliases on.</param>
        private static void CreateDynamicAliases(XPClassInfo classInfo)
        {
            foreach (var alias in AliasedFieldMappings.Where(a => a.OwnerType == classInfo.ClassType && a.OwnerType == a.DeclaringType))
            {
                classInfo.CreateAliasedMember(alias);
            }
        }

        #endregion
    }
}
