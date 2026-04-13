using System.Collections.Generic;
using DevExpress.Data.Filtering;

namespace TIG.TotalLink.Client.Core.Extension.CriteriaVisitor
{
    /// <summary>
    /// Replaces all criteria that operate on properties that are included in the propertyReplacements list.
    /// </summary>
    public class ReplacePropertiesCriteriaVisitor : IClientCriteriaVisitor<CriteriaOperator>
    {
        #region Private Fields

        private Dictionary<string, string> _propertyReplacements;

        #endregion


        #region Public Methods

        /// <summary>
        /// Starts executing this CriteriaVisitor.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to replace properties in.</param>
        /// <param name="propertyReplacements">A list of replacements to make.  The Key is the property to search for, and the Value is the the property to replace it with.</param>
        /// <returns>A CriteriaOperator with all the specified properties replaced.</returns>
        public CriteriaOperator Start(CriteriaOperator criteriaOperator, Dictionary<string, string> propertyReplacements)
        {
            // Abort if no criteriaOperator or propertyReplacements were supplied
            if (ReferenceEquals(criteriaOperator, null) || propertyReplacements == null || propertyReplacements.Count == 0)
                return null;

            // Store local variables
            _propertyReplacements = propertyReplacements;

            // Execute the CriteriaVisitor
            return criteriaOperator.Accept(this);
        }

        #endregion


        #region IClientCriteriaVisitor

        public CriteriaOperator Visit(JoinOperand theOperand)
        {
            var condition = theOperand.Condition.Accept(this);
            var expression = theOperand.AggregatedExpression.Accept(this);
            if (ReferenceEquals(condition, null) || ReferenceEquals(expression, null))
                return null;

            return new JoinOperand(theOperand.JoinTypeName, condition, theOperand.AggregateType, expression);
        }

        public CriteriaOperator Visit(OperandProperty theOperand)
        {
            // If the old property exists in the replacements list, return a new OperandProperty containing the new property
            string newPropertyName;
            if (_propertyReplacements.TryGetValue(theOperand.PropertyName, out newPropertyName))
                return new OperandProperty(newPropertyName);

            // Return the OperandProperty unmodified
            return theOperand;
        }

        public CriteriaOperator Visit(AggregateOperand theOperand)
        {
            var operand = theOperand.CollectionProperty.Accept(this) as OperandProperty;
            var condition = theOperand.Condition.Accept(this);
            var expression = theOperand.AggregatedExpression.Accept(this);
            if (ReferenceEquals(condition, null) || ReferenceEquals(expression, null) || ReferenceEquals(operand, null))
                return null;

            return new AggregateOperand(operand, expression, theOperand.AggregateType, condition);
        }

        public CriteriaOperator Visit(FunctionOperator theOperator)
        {
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    return null;

                operators.Add(temp);
            }

            return new FunctionOperator(theOperator.OperatorType, operators);
        }

        public CriteriaOperator Visit(OperandValue theOperand)
        {
            return theOperand;
        }

        public CriteriaOperator Visit(GroupOperator theOperator)
        {
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    continue;

                operators.Add(temp);
            }

            return new GroupOperator(theOperator.OperatorType, operators);
        }

        public CriteriaOperator Visit(InOperator theOperator)
        {
            var leftOperand = theOperator.LeftOperand.Accept(this);
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    continue;

                operators.Add(temp);
            }

            if (ReferenceEquals(leftOperand, null))
                return null;

            return new InOperator(leftOperand, operators);
        }

        public CriteriaOperator Visit(UnaryOperator theOperator)
        {
            var operand = theOperator.Operand.Accept(this);
            if (ReferenceEquals(operand, null))
                return null;

            return new UnaryOperator(theOperator.OperatorType, operand);
        }

        public CriteriaOperator Visit(BinaryOperator theOperator)
        {
            var leftOperand = theOperator.LeftOperand.Accept(this);
            var rightOperand = theOperator.RightOperand.Accept(this);
            if (ReferenceEquals(leftOperand, null) || ReferenceEquals(rightOperand, null))
                return null;

            return new BinaryOperator(leftOperand, rightOperand, theOperator.OperatorType);
        }

        public CriteriaOperator Visit(BetweenOperator theOperator)
        {
            var test = theOperator.TestExpression.Accept(this);
            var begin = theOperator.BeginExpression.Accept(this);
            var end = theOperator.EndExpression.Accept(this);
            if (ReferenceEquals(test, null) || ReferenceEquals(begin, null) || ReferenceEquals(end, null))
                return null;

            return new BetweenOperator(test, begin, end);
        }

        #endregion
    }
}
