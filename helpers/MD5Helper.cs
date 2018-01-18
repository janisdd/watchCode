using System.Security.Cryptography;
using System.Text;

namespace watchCode.helpers
{
    public static class MD5Helper
    {
        
        static readonly MD5 md5 = MD5.Create();

        public static string GetHash(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes (text);
            byte[] result = md5.ComputeHash(bytes);

            return System.BitConverter.ToString(result).Replace("-", "").ToLower();
        }
    }
}