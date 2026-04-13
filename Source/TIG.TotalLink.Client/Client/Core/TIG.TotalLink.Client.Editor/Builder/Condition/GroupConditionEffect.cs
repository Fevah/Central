namespace TIG.TotalLink.Client.Editor.Builder.Condition
{
    public class GroupConditionEffect : ConditionEffectBase
    {
        #region Constructors

        public GroupConditionEffect(VisualEffects effect, string groupName)
            : base(effect)
        {
            GroupName = groupName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the group that is affected.
        /// </summary>
        public string GroupName { get; private set; }
        
        #endregion
    }
}
