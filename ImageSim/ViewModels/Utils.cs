using System;
using System.IO;
using System.Threading.Tasks;

namespace ImageSim.ViewModels
{
    public static class Utils
    {
        public static string GetFileHash(string path)
        {
            System.Threading.Thread.Sleep(300); //FIXME
            using var fs = File.OpenRead(path);
            var alg = System.Security.Cryptography.MD5.Create();
            var hash = alg.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
        }

        public static Task<string> GetFileHashAsync(string path)
        {
            return Task.Run(() => GetFileHash(path));
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
