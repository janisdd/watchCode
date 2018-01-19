using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace watchCode.helpers
{
    public static class HashHelper
    {
        private static readonly MD5 md5 = MD5.Create();
        private static readonly SHA256 sha256 = SHA256.Create();
        private static readonly SHA512 sha512 = SHA512.Create();

        private static string DefualtHashAlgorithmName = "sha512";

        private static HashAlgorithm hashAlgo = sha512;

        public static void SetHashAlgorithm(string name)
        {
            switch (name)
            {
                case "md5":
                    hashAlgo = md5;
                    break;
                case "sha256":
                    hashAlgo = sha256;
                    break;
                case "sha512":
                    hashAlgo = sha512;
                    break;
                default:
                    Logger.Error($"unknown hash algorith set, setting to default: {DefualtHashAlgorithmName}");
                    hashAlgo = sha512;
                    break;
            }
        }

        public static string GetHash(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return BitConverter.ToString(hashAlgo.ComputeHash(bytes)).Replace("-", "").ToLower();
        }

        /// <summary>
        /// gets the md5 hash of the given file info
        /// 
        /// <remarks>
        /// TOOD i think this is better for large files because calling
        /// ComputeHash of a stream will calculate the md5 in chunks, so no need to
        /// store the whole file in memory??
        /// </remarks>
        /// </summary>
        /// <param name="info"></param>
        /// <returns>the md5 hash nor null on some error</returns>
        public static string GetHashForFile(FileInfo info)
        {
            if (info.Exists == false)
            {
                Logger.Error($"cannot get hash of not existing file: {info.FullName}");
                return null;
            }

            string hash = null;

            try
            {
                using (var fileStream = info.Open(FileMode.Open, FileAccess.Read))
                {
                    hash = BitConverter.ToString(hashAlgo.ComputeHash(fileStream)).Replace("-", "").ToLower();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"cannot get hash of file: {info.FullName}, error: {e.Message}");
                return null;
            }

            return hash;
        }


        /// <summary>
        /// we may use another hash algorithm for file name ... to get shorter names
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetHashForFileName(string fileName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(fileName);
            return BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLower();
        }
    }
}