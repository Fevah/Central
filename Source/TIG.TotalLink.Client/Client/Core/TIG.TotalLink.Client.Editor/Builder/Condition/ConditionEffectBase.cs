namespace TIG.TotalLink.Client.Editor.Builder.Condition
{
    public abstract class ConditionEffectBase
    {
        #region Public Enums

        public enum VisualEffects
        {
            Visibility,
            Enabled,
            Required,
            ReadOnly,
            InstanceMethod
        }

        #endregion


        #region Constructors

        protected ConditionEffectBase(VisualEffects effect)
        {
            Effect = effect;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The effect applied to the property.
        /// </summary>
        public VisualEffects Effect { get; private set; }

        #endregion
    }
}
