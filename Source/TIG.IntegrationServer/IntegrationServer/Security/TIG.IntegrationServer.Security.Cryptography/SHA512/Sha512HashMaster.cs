using System.IO;
using System.Text;

namespace TIG.IntegrationServer.Security.Cryptography.SHA512
{
    public class Sha512HashMaster : HashMaster
    {
        #region Overrides

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        public override byte[] GetHash(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = GetHash(bytes);
            return hash;
        }

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        public override byte[] GetHash(Stream content)
        {
            using (var hasher = System.Security.Cryptography.SHA512.Create())
            {
                var hash = hasher.ComputeHash(content);
                return hash;
            }
        }

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Hash of byte[]</returns>
        public override byte[] GetHash(byte[] content)
        {
            using (var hasher = System.Security.Cryptography.SHA512.Create())
            {
                var hash = hasher.ComputeHash(content);
                return hash;
            }
        }

        #endregion
    }
}
