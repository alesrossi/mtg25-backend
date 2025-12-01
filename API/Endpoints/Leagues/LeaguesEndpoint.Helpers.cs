using System.Security.Cryptography;
using System.Text;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static class SecureCodeGenerator
    {
        private static readonly char[] Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        public static string GenerateCode(int length = 6)
        {
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(Chars[b % Chars.Length]);
            }

            return result.ToString();
        }
    }
}
