using System;
using System.IO;
using System.Security.AccessControl;

namespace TIG.IntegrationServer.Security.Cryptography
{
    public abstract class HashMaster : IHashMaster
    {
        #region Abstract Methods

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        public abstract byte[] GetHash(string content);

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        public abstract byte[] GetHash(Stream content);

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Hash of byte[]</returns>
        public abstract byte[] GetHash(byte[] content);

        #endregion


        #region Public Methods

        /// <summary>
        /// Get hash of file
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Hash of byte[]</returns>
        public byte[] GetHashOfFile(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileSystemRights.Read, FileShare.Read, 1024,
                    FileOptions.SequentialScan))
            {
                var hash = GetHash(stream);
                return hash;
            }
        }

        /// <summary>
        /// Get hash of file
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Hash of byte[]</returns>
        public byte[] GetHashOfFile(FileInfo fileInfo)
        {
            var hash = GetHashOfFile(fileInfo.FullName);
            return hash;
        }

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        public string GetHashAsHexString(string content)
        {
            var hash = GetHash(content);
            var hex = ConvertToHexString(hash);
            return hex;
        }

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        public string GetHashAsHexString(Stream content)
        {
            var hash = GetHash(content);
            var hex = ConvertToHexString(hash);
            return hex;
        }

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        public string GetHashAsHexString(byte[] content)
        {
            var hash = GetHash(content);
            var hex = ConvertToHexString(hash);
            return hex;
        }

        /// <summary>
        /// Get hash as hex string by file path
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Hash hex Context</returns>
        public string GetHashOfFileAsHexString(string filePath)
        {
            var hash = GetHashOfFile(filePath);
            var hex = ConvertToHexString(hash);
            return hex;
        }

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Hash hex Context</returns>
        public string GetHashOfFileAsHexString(FileInfo fileInfo)
        {
            var hash = GetHashOfFile(fileInfo);
            var hex = ConvertToHexString(hash);
            return hex;
        }

        /// <summary>
        /// Get hash as base 64 value
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>base 64 value</returns>
        public string GetHashAsBase64String(string content)
        {
            var hash = GetHash(content);
            var base64 = ConvertToBase64String(hash);
            return base64;
        }

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Base 64 value</returns>
        public string GetHashAsBase64String(Stream content)
        {
            var hash = GetHash(content);
            var base64 = ConvertToBase64String(hash);
            return base64;
        }

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Base 64 value</returns>
        public string GetHashAsBase64String(byte[] content)
        {
            var hash = GetHash(content);
            var base64 = ConvertToBase64String(hash);
            return base64;
        }

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Base 64 value</returns>
        public string GetHashOfFileAsBase64String(string filePath)
        {
            var hash = GetHashOfFile(filePath);
            var base64 = ConvertToBase64String(hash);
            return base64;
        }

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Base 64 value</returns>
        public string GetHashOfFileAsBase64String(FileInfo fileInfo)
        {
            var hash = GetHashOfFile(fileInfo);
            var base64 = ConvertToBase64String(hash);
            return base64;
        }

        /// <summary>
        /// Convert to hex string by bytes value
        /// </summary>
        /// <param name="bytes">Byte[] content, it use to calculate hex</param>
        /// <returns>Hex value</returns>
        public string ConvertToHexString(byte[] bytes)
        {
            var hex = BitConverter.ToString(bytes);
            return hex.Replace("-", string.Empty).ToUpper();
        }

        /// <summary>
        /// Convert to base 64 string by bytes value
        /// </summary>
        /// <param name="bytes">Byte[] content, it use to calculate hex</param>
        /// <returns>Base 64 value</returns>
        public string ConvertToBase64String(byte[] bytes)
        {
            var hex = Convert.ToBase64String(bytes);
            return hex;
        }

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="filePath">Path of first hash</param>
        /// <param name="expectedHexedHash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        public bool FileHasExpectedHashHex(string filePath, string expectedHexedHash)
        {
            var actualHexedHash = GetHashOfFileAsHexString(filePath);
            var result = HashHexesAreEqual(actualHexedHash, expectedHexedHash);
            return result;
        }

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="file">File info of first hash</param>
        /// <param name="expectedHexedHash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        public bool FileHasExpectedHashHex(FileInfo file, string expectedHexedHash)
        {
            var result = FileHasExpectedHashHex(file.FullName, expectedHexedHash);
            return result;
        }

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="filePath">Path of first hash</param>
        /// <param name="expectedBase64Hash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        public bool FileHasExpectedBase64Hash(string filePath, string expectedBase64Hash)
        {
            var actualBase64Hash = GetHashOfFileAsBase64String(filePath);
            var result = Base64HashesAreEqual(actualBase64Hash, expectedBase64Hash);
            return result;
        }

        /// <summary>
        /// Compare two base 64 hash value
        /// </summary>
        /// <param name="file">File info of first hash</param>
        /// <param name="expectedBase64Hash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two base 64 hash match</returns>
        public bool FileHasExpectedBase64Hash(FileInfo file, string expectedBase64Hash)
        {
            var result = FileHasExpectedBase64Hash(file.FullName, expectedBase64Hash);
            return result;
        }

        /// <summary>
        /// Compare hash hex
        /// </summary>
        /// <param name="hexedHash1">hash source</param>
        /// <param name="hexedHash2">hash target</param>
        /// <returns>True, if two hash values are equal</returns>
        public bool HashHexesAreEqual(string hexedHash1, string hexedHash2)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var compareResult = comparer.Compare(hexedHash1, hexedHash2);
            return compareResult == 0;
        }

        /// <summary>
        /// Compare base64 hash
        /// </summary>
        /// <param name="base64Hash1">Base 64 hash source</param>
        /// <param name="base64Hash2">Base 64 hash target</param>
        /// <returns>True, if two hash values are equal</returns>
        public bool Base64HashesAreEqual(string base64Hash1, string base64Hash2)
        {
            var comparer = StringComparer.Ordinal;
            var compareResult = comparer.Compare(base64Hash1, base64Hash2);
            return compareResult == 0;
        }

        #endregion
    }
}
