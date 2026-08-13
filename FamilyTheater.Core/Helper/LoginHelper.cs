using System.Security.Cryptography;
using System.Text;

namespace FamilyTheater.Core.Helper
{
    public static class LoginHelper
    {
        private const int SaltSize = 16;      // 128 bits
        private const int KeySize = 32;       // 256 bits
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;
        private const char SegmentDelimiter = ':';

        /// <summary>
        /// 验证明文密码是否与存储的哈希值匹配
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                // 1. 解析存储的哈希字符串（格式：Iterations:Salt:Key）
                var segments = storedHash.Split(SegmentDelimiter);
                if (segments.Length != 3) return false;

                var iterations = int.Parse(segments[0]);
                var salt = Convert.FromBase64String(segments[1]);
                var key = Convert.FromBase64String(segments[2]);

                // 2. 使用相同的盐、迭代次数和算法重新计算哈希
                using var deriveBytes = new Rfc2898DeriveBytes(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    Algorithm);

                var computedKey = deriveBytes.GetBytes(KeySize);

                // 3. 使用时间恒定的比较防止时序攻击
                return CryptographicOperations.FixedTimeEquals(computedKey, key);
            }
            catch
            {
                // 哈希格式损坏或解析失败，一律返回 false
                return false;
            }
        }

        /// <summary>
        /// 生成密码哈希（确保此方法与 VerifyPassword 的参数完全对应）
        /// </summary>
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            using var deriveBytes = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                Algorithm);

            var key = deriveBytes.GetBytes(KeySize);

            // 格式：迭代次数:盐(Base64):密钥(Base64)
            return $"{Iterations}{SegmentDelimiter}" +
                   $"{Convert.ToBase64String(salt)}{SegmentDelimiter}" +
                   $"{Convert.ToBase64String(key)}";
        }
    }
}
