using System.IO;

namespace TIG.IntegrationServer.Security.Cryptography
{
    public interface IHashMaster
    {
        #region  Methods

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        byte[] GetHash(string content);

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Hash byte[]</returns>
        byte[] GetHash(Stream content);

        /// <summary>
        /// Get hash by string content
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Hash of byte[]</returns>
        byte[] GetHash(byte[] content);

        #endregion


        #region Methods

        /// <summary>
        /// Get hash of file
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Hash of byte[]</returns>
        byte[] GetHashOfFile(string filePath);

        /// <summary>
        /// Get hash of file
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Hash of byte[]</returns>
        byte[] GetHashOfFile(FileInfo fileInfo);

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        string GetHashAsHexString(string content);

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        string GetHashAsHexString(Stream content);

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Hash hex Context</returns>
        string GetHashAsHexString(byte[] content);

        /// <summary>
        /// Get hash as hex string by file path
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Hash hex Context</returns>
        string GetHashOfFileAsHexString(string filePath);

        /// <summary>
        /// Get hash as hex string by string content
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Hash hex Context</returns>
        string GetHashOfFileAsHexString(FileInfo fileInfo);

        /// <summary>
        /// Get hash as base 64 value
        /// </summary>
        /// <param name="content">String content, it need to be calculate hash</param>
        /// <returns>base 64 value</returns>
        string GetHashAsBase64String(string content);

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="content">Stream content, it need to be calculate hash</param>
        /// <returns>Base 64 value</returns>
        string GetHashAsBase64String(Stream content);

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="content">Byte[] content, it need to be calculate hash</param>
        /// <returns>Base 64 value</returns>
        string GetHashAsBase64String(byte[] content);

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="filePath">File path for get hash</param>
        /// <returns>Base 64 value</returns>
        string GetHashOfFileAsBase64String(string filePath);

        /// <summary>
        /// Get has as base 64 value
        /// </summary>
        /// <param name="fileInfo">File info for get hash</param>
        /// <returns>Base 64 value</returns>
        string GetHashOfFileAsBase64String(FileInfo fileInfo);

        /// <summary>
        /// Convert to hex string by bytes value
        /// </summary>
        /// <param name="bytes">Byte[] content, it use to calculate hex</param>
        /// <returns>Hex value</returns>
        string ConvertToHexString(byte[] bytes);

        /// <summary>
        /// Convert to base 64 string by bytes value
        /// </summary>
        /// <param name="bytes">Byte[] content, it use to calculate hex</param>
        /// <returns>Base 64 value</returns>
        string ConvertToBase64String(byte[] bytes);

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="filePath">Path of first hash</param>
        /// <param name="expectedHexedHash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        bool FileHasExpectedHashHex(string filePath, string expectedHexedHash);

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="file">File info of first hash</param>
        /// <param name="expectedHexedHash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        bool FileHasExpectedHashHex(FileInfo file, string expectedHexedHash);

        /// <summary>
        /// Compare two hash value
        /// </summary>
        /// <param name="filePath">Path of first hash</param>
        /// <param name="expectedBase64Hash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two hash match</returns>
        bool FileHasExpectedBase64Hash(string filePath, string expectedBase64Hash);

        /// <summary>
        /// Compare two base 64 hash value
        /// </summary>
        /// <param name="file">File info of first hash</param>
        /// <param name="expectedBase64Hash">Target expected hexed hash.</param>
        /// <returns>True, indicate this two base 64 hash match</returns>
        bool FileHasExpectedBase64Hash(FileInfo file, string expectedBase64Hash);

        /// <summary>
        /// Compare hash hex
        /// </summary>
        /// <param name="hexedHash1">hash source</param>
        /// <param name="hexedHash2">hash target</param>
        /// <returns>True, if two hash values are equal</returns>
        bool HashHexesAreEqual(string hexedHash1, string hexedHash2);

        /// <summary>
        /// Compare base64 hash
        /// </summary>
        /// <param name="base64Hash1">Base 64 hash source</param>
        /// <param name="base64Hash2">Base 64 hash target</param>
        /// <returns>True, if two hash values are equal</returns>
        bool Base64HashesAreEqual(string base64Hash1, string base64Hash2);

        #endregion
    }
}
