using System.Text;

namespace Veeam.Signature
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] data)
        {
            const string hex = "0123456789ABCDEF";
            var sb = new StringBuilder(data.Length * 2);

            foreach (byte b in data)
            {
                sb.Append(hex[b >> 0x4]);
                sb.Append(hex[b & 0xF]);
            }

            return sb.ToString();
        }
    }
}
