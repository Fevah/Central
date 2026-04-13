using System;
using System.Collections.Generic;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.Constant;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.Expression;

namespace TIG.IntegrationServer.Plugin.Converter.EntityConverter
{
    public class EntityConverter : IConverterPlugin
    {
        /// <summary>
        /// Converter for get entity by entity key.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            // Build real expression based on source entity.
            var expression = expressionDescriptor.BuildExpression();

            // Get entity key information.
            var entityInfo = expression.Split(',');
            if (entityInfo.Length != 3)
            {
                return null;
            }
            var key = entityInfo[0].Trim();
            var value = entityInfo[1].Trim();
            var keyType = Type.GetType(entityInfo[2].Trim());

            // Get entity by entity key information.
            var filter = new BinaryOperator(new QueryOperand(key, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(keyType)),
                new ParameterValue { Value = value }, BinaryOperatorType.Equal);
            return string.IsNullOrEmpty(value) ? null : targetAgent.ReadOne(type, new List<EntityFieldInfo>(), filter);
        }
    }
}
