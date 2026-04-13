using System.Reflection;

namespace TIG.TotalLink.Client.Editor.Builder.Condition
{
    public class PropertyConditionEffect : ConditionEffectBase
    {
        #region Constructors

        public PropertyConditionEffect(VisualEffects effect, PropertyInfo property)
            : base(effect)
        {
            Property = property;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The property that is affected.
        /// </summary>
        public PropertyInfo Property { get; private set; }
        
        #endregion
    }
}
