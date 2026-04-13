using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Reflection;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Data.Linq;
using DevExpress.Data.Linq.Helpers;
using DevExpress.Xpo.DB;
using LinqToLdap;
using TIG.TotalLink.Server.DataService.ActiveDirectory.Extension;
using TIG.TotalLink.Shared.DataModel.ActiveDirectory;
using TIG.TotalLink.Shared.DataModel.ActiveDirectory.Provider;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Server.DataService.ActiveDirectory.Provider
{
    public class ActiveDirectoryProvider : IDataStore
    {
        #region Private Fields

        private static readonly object _defaultValuesLock = new object();
        private static readonly Dictionary<string, object> _defaultValues = new Dictionary<string, object>()
        {
            {"OptimisticLockField", 0},
            {"GCRecord", null}
        };
        private static readonly object _propertyCacheLock = new object();
        private static readonly Dictionary<string, PropertyInfo> _propertyCache = new Dictionary<string, PropertyInfo>();

        private readonly IDirectoryContext _context;
        private readonly Assembly _dataModelAssembly;
        private readonly MethodInfo _executeAdQueryMethod;

        #endregion


        #region Constructors

        public ActiveDirectoryProvider()
        {
            // Create a context provider and get the context
            var contextProvider = new ActiveDirectoryContextProvider();
            _context = contextProvider.Context;

            // Store data for dynamic processes later
            Func<SelectStatement, SelectStatementResult> executeAdQueryMethodSig = ExecuteAdQuery<ActiveDirectoryUser>;
            _executeAdQueryMethod = GetType().GetMethod(executeAdQueryMethodSig.Method.Name, BindingFlags.Instance | BindingFlags.NonPublic);
            _dataModelAssembly = typeof(ActiveDirectoryUser).Assembly;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Executes an Active Directory query as returns the results.
        /// </summary>
        /// <typeparam name="T">The type of object to query for.</typeparam>
        /// <param name="selectStatement">A SelectStatement containing details about the query.</param>
        /// <returns>The results of the query as a SelectStatementResult.</returns>
        private SelectStatementResult ExecuteAdQuery<T>(SelectStatement selectStatement)
            where T : class
        {
            EnsureDomainNameInitialized();

            var resultRows = new List<SelectStatementResultRow>();

            try
            {
                // Create the initial query
                var adQuery = _context.Query<T>();
                var converter = new CriteriaToExpressionConverter();

                // Apply the condition
                var condition = selectStatement.Condition.ConvertQuery();
                if (condition != null)
                    adQuery = (IQueryable<T>)adQuery.AppendWhere(converter, condition);

                // Special handling for global count query
                var subQuery = selectStatement.Operands[0] as QuerySubQueryContainer;
                if (!ReferenceEquals(subQuery, null) && subQuery.AggregateType == Aggregate.Count)
                    return new SelectStatementResult(adQuery.Count());

                // Resolve the AD query before applying sorting
                // Because LDAP sorting is limited and inefficient
                var localQuery = adQuery.ToList().AsQueryable();

                // Apply sorting
                if (selectStatement.SortProperties.Any())
                {
                    var sortDescriptors = selectStatement.SortProperties
                        .Select(i => new ServerModeOrderDescriptor(new OperandProperty(((QueryOperand)i.Property).ColumnName), (i.Direction == SortingDirection.Descending)))
                        .ToArray();
                    localQuery = (IQueryable<T>)localQuery.MakeOrderBy(converter, sortDescriptors);
                }

                // Apply the skip
                if (selectStatement.SkipSelectedRecords > 0)
                {
                    localQuery = localQuery.Skip(selectStatement.SkipSelectedRecords);
                }

                // Apply the take
                if (selectStatement.TopSelectedRecords > 0)
                {
                    localQuery = localQuery.Take(selectStatement.TopSelectedRecords);
                }

                // Resolve the local query to get the final results
                var queryRows = localQuery.ToList();

                // Process each query result and convert it to a select result...
                foreach (var queryRow in queryRows)
                {
                    var resultValues = new List<object>();

                    // Process each operand (property) in the select...
                    foreach (var operand in selectStatement.Operands)
                    {
                        var operand1 = operand;
                        var queryRow1 = queryRow;
                        TypeSwitch.On(operand.GetType())
                            .Case<QueryOperand>(() =>
                            {
                                resultValues.Add(GetPropertyValue(queryRow1, ((QueryOperand)operand1).ColumnName));
                            })
                            .Case<QuerySubQueryContainer>(() =>
                            {
                                resultValues.Add(0);
                            });
                    }

                    // Add the new row to the results
                    resultRows.Add(new SelectStatementResultRow(resultValues.ToArray()));
                }
            }
            catch (NotSupportedException ex)
            {
                throw new Exception("Sorting, grouping and filtering on aliased columns is not supported.");
            }
            catch (Exception ex)
            {
                throw;
            }

            return new SelectStatementResult(resultRows);
        }

        /// <summary>
        /// Returns a default value or property value for an object.
        /// </summary>
        /// <param name="obj">The object to retrieve the value from.</param>
        /// <param name="propertyName">The name of the property to retrieve.</param>
        /// <returns>THe value of the specified property.</returns>
        private object GetPropertyValue(object obj, string propertyName)
        {
            // Attempt to get a default value for the propertyName
            lock (_defaultValuesLock)
            {
                object defaultValue;
                if (_defaultValues.TryGetValue(propertyName, out defaultValue))
                    return defaultValue;
            }

            // Attempt to get a property for the propertyName
            lock (_propertyCacheLock)
            {
                var type = obj.GetType();
                var propertyKey = string.Format("{0}.{1}", type.FullName, propertyName);

                PropertyInfo property;
                if (!_propertyCache.TryGetValue(propertyKey, out property))
                {
                    // If no property was found in the cache, attempt to add it
                    property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (property != null)
                        _propertyCache.Add(propertyKey, property);
                }

                // Return the property value (or null if no property was found)
                return (property != null ? property.GetValue(obj) : null);
            }
        }

        /// <summary>
        /// Ensures the DomainName has been added to the default values.
        /// </summary>
        private void EnsureDomainNameInitialized()
        {
            lock (_defaultValuesLock)
            {
                // Abort if the DomainName has already been stored
                if (_defaultValues.ContainsKey("DomainName"))
                    return;

                // Get the distinguished name of the domain root
                var rootDn = _context.ListServerAttributes("rootdomainnamingcontext").Single().Value as string;

                // Get the domain name of the domain root and add it to the defaultValues list
                var example = new
                {
                    DistinguishedName = "",
                    Name = ""
                };

                try
                {
                    var domain = _context.Query(example, rootDn, "domain").FirstOrDefault(d => d.DistinguishedName == rootDn);
                    _defaultValues.Add("DomainName", (domain != null ? domain.Name : null));
                }
                catch (DirectoryOperationException ex)
                {
                    // Ignore directory read errors
                }
            }
        }

        #endregion


        #region IDataStore

        public UpdateSchemaResult UpdateSchema(bool dontCreateIfFirstTableNotExist, params DBTable[] tables)
        {
            return UpdateSchemaResult.SchemaExists;
        }

        public SelectedData SelectData(params SelectStatement[] selects)
        {
            var resultSet = new List<SelectStatementResult>();

            foreach (var selectStatement in selects)
            {
                // Queries for XPObjectType will return a list of all objects in the AD datamodel assembly which derive from DataObjectBase
                if (selectStatement.TableName == "XPObjectType")
                {
                    var adTypes = _dataModelAssembly.GetTypes()
                            .Where(t => typeof(DataObjectBase).IsAssignableFrom(t) && !t.IsAbstract)
                            .ToList();
                    var index = 1;
                    foreach (var adType in adTypes)
                    {
                        resultSet.Add(new SelectStatementResult(index++, adType.FullName, adType.Assembly.GetName().Name));
                    }
                    continue;
                }

                // All other queries will be executed on the AD context
                var queryType = _dataModelAssembly.GetType(string.Format("{0}.{1}", _dataModelAssembly.GetName().Name, selectStatement.TableName));
                var genericExecuteAdQueryMethod = _executeAdQueryMethod.MakeGenericMethod(queryType);
                resultSet.Add(genericExecuteAdQueryMethod.Invoke(this, new object[] { selectStatement }) as SelectStatementResult);
            }

            return new SelectedData(resultSet.ToArray());
        }

        public ModificationResult ModifyData(params ModificationStatement[] dmlStatements)
        {
            throw new InvalidOperationException("This DataStore does not support modifications.");
        }

        public AutoCreateOption AutoCreateOption
        {
            get { return AutoCreateOption.SchemaAlreadyExists; }
        }

        #endregion
    }

}