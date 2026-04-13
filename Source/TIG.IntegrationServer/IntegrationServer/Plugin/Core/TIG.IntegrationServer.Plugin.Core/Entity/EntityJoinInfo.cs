using System.Collections.Generic;
using System.Linq;

namespace TIG.IntegrationServer.Plugin.Core.Entity
{
    public class EntityJoinInfo
    {
        #region Public Properties

        /// <summary>
        /// Joined entity name
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// Joined entity type
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Alias for persistence query.
        /// System will automatic generated
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Join key for persistence join main table and joined table.
        /// </summary>
        public string JoinKey { get; set; }

        /// <summary>
        /// Field infos of joined entity
        /// </summary>
        public List<EntityFieldInfo> FieldInfos { get; set; }

        /// <summary>
        /// Extra entity info in current joined entity
        /// </summary>
        public List<EntityJoinInfo> SubJoinEntityInfos { get; set; }

        #endregion


        #region Override Methods

        /// <summary>
        /// ToString will automatic convert to odata format.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (SubJoinEntityInfos == null || !SubJoinEntityInfos.Any())
                return EntityName;

            var subExpand = string.Join("/", SubJoinEntityInfos.Select(p => p.EntityName));
            return string.Join("/", EntityName, subExpand);
        }

        #endregion
    }
}