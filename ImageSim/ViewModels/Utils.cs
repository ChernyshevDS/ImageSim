using System;
using System.IO;
using System.Threading.Tasks;

namespace ImageSim.ViewModels
{
    public static class Utils
    {
        public static byte[] GetFileHash(string path)
        {
            using var fs = File.OpenRead(path);
            var alg = System.Security.Cryptography.MD5.Create();
            return alg.ComputeHash(fs);
        }

        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty).ToUpperInvariant();
        }

        public static int Clamp(this int val, int min, int max)
        {
            return Math.Max(min, Math.Min(val, max));
        }

        public static int GetRecommendedConcurrencyLevel()
        {
            return 4;   //TODO
        }
    }
}
