using System.Collections.Generic;
using DevExpress.Data.Filtering;

namespace TIG.TotalLink.Client.Core.Extension.CriteriaVisitor
{
    /// <summary>
    /// Replaces all criteria from groups where the contained criteria matches the removeOperator with the replaceOperator.
    /// </summary>
    public class ReplaceGroupedOperatorCriteriaVisitor : IClientCriteriaVisitor<CriteriaOperator>
    {
        #region Private Fields

        private CriteriaOperator _removeOperator;
        private CriteriaOperator _replaceOperator;

        #endregion
        

        #region Public Methods

        /// <summary>
        /// Starts executing this CriteriaVisitor.
        /// </summary>
        /// <param name="criteriaOperator">The CriteriaOperator to remove the <paramref name="removeOperator"/> from.</param>
        /// <param name="removeOperator">The CriteriaOperator to remove.</param>
        /// <param name="replaceOperator">The CriteriaOperator to replace the <paramref name="removeOperator"/> with.</param>
        /// <returns>A CriteriaOperator with the <paramref name="removeOperator"/> replaced.</returns>
        public CriteriaOperator Start(CriteriaOperator criteriaOperator, CriteriaOperator removeOperator, CriteriaOperator replaceOperator)
        {
            // Abort if no criteriaOperator or removeOperator were supplied
            if (ReferenceEquals(criteriaOperator, null) || ReferenceEquals(removeOperator, null))
                return null;

            // Store local variables
            _removeOperator = removeOperator;
            _replaceOperator = replaceOperator;

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
            // Process each operator contained in the group
            var operators = new List<CriteriaOperator>();
            foreach (var op in theOperator.Operands)
            {
                // Abort if the contained operator is null
                var temp = op.Accept(this);
                if (ReferenceEquals(temp, null))
                    continue;

                // If the contained operator matches the removeOperator, add a clone of the replaceOperator instead
                if (Equals(temp, _removeOperator))
                {
                    operators.Add(CriteriaOperator.Clone(_replaceOperator));
                    continue;
                }

                // Add the operator to the new list
                operators.Add(temp);
            }

            // Return a new group containing the new list of operators
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
