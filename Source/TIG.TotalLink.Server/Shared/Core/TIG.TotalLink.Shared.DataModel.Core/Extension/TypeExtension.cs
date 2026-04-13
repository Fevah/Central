using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class TypeExtension
    {
        #region Public Methods

        /// <summary>
        /// Determines whether an instance of the current generic <see cref="T:System.Type"/> can be assigned from an instance of the specified Type.
        /// Note: This will not work with generic interfaces.
        /// </summary>
        /// <param name="type">The generic type to compare to.</param>
        /// <param name="compareType">The type to be compared.</param>
        /// <returns>True if compareType can be assigned to type.</returns>
        public static bool IsAssignableFromGeneric(this Type type, Type compareType)
        {
            while (!type.IsAssignableFrom(compareType))
            {
                if (compareType == null || compareType == typeof(object))
                {
                    return false;
                }

                if (compareType.IsGenericType && !compareType.IsGenericTypeDefinition)
                {
                    compareType = compareType.GetGenericTypeDefinition();
                }
                else
                {
                    compareType = compareType.BaseType;
                }
            }

            return true;
        }

        #endregion
    }
}
