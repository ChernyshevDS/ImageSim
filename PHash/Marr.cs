using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using OpenCvSharp;

[assembly: InternalsVisibleTo("Tests")]
namespace PHash
{
    public static class Marr
    {
        internal class CachedData
        {
            public float Alpha;
            public float Level;
            public Mat Kernel;
        }

        private static bool AlmostEquals(this float value, float other, float eps = float.Epsilon)
        {
            return MathF.Abs(value - other) < eps;
        }

        private static ThreadLocal<CachedData> LastRunCache = null;

        private static Mat GetMHKernelCached(float alpha, float level)
        {
            if (LastRunCache == null)
            {
                LastRunCache = new ThreadLocal<CachedData>(() => new CachedData());
            }

            if (!LastRunCache.Value.Alpha.AlmostEquals(alpha, 1e-6f)
                || !LastRunCache.Value.Level.AlmostEquals(level, 1e-6f))
            {
                
                LastRunCache.Value.Alpha = alpha;
                LastRunCache.Value.Level = level;
                LastRunCache.Value.Kernel = GetMHKernel(alpha, level);
            }
            return LastRunCache.Value.Kernel;
        }

        public static byte[] GetImageHash(string filename, float alpha = 2.0f, float lvl = 1.0f)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            using var fs = new System.IO.FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using var src = Mat.FromStream(fs, ImreadModes.Color);

            Mat img = null;
            if (src.Channels() == 3)
            {
                var tmp = Utils.GetBrightnessComponent(src);
                Cv2.GaussianBlur(tmp, tmp, Size.Zero, 1.0, 1.0, BorderTypes.Replicate);
                tmp = tmp.Resize(new Size(512, 512), 0, 0, InterpolationFlags.Cubic);
                Cv2.EqualizeHist(tmp, tmp);
                img = tmp;
            }
            else
            {
                var tmp = src.ExtractChannel(0);
                Cv2.GaussianBlur(tmp, tmp, Size.Zero, 1.0, 1.0, BorderTypes.Replicate);
                tmp = tmp.Resize(new Size(512, 512), 0, 0, InterpolationFlags.Cubic);
                Cv2.EqualizeHist(tmp, tmp);
                img = tmp;
            }
            var pMHKernel = GetMHKernelCached(alpha, lvl);

            using var img_f = new Mat<float>(img.Size());
            img.ConvertTo(img_f, img_f.Type(), 1 / 255.0);

            var fresp = new Mat(img.Size(), MatType.CV_32FC1);
            Cv2.Filter2D(img, fresp, MatType.CV_32FC1, pMHKernel, null, 0, BorderTypes.Replicate);

            fresp = fresp.Normalize(0, 1, NormTypes.MinMax);

            var blocks = new Mat<float>(31, 31, new Scalar(0));
            var blocks_i = blocks.GetIndexer();
            for (int rindex = 0; rindex < 31; rindex++)
            {
                for (int cindex = 0; cindex < 31; cindex++)
                {
                    var x0 = rindex * 16;
                    var y0 = cindex * 16;
                    var x1 = rindex * 16 + 16 - 1;
                    var y1 = cindex * 16 + 16 - 1;
                    blocks_i[cindex, rindex] = (float)fresp.SubMat(y0, y1 + 1, x0, x1 + 1).Sum().Val0;
                }
            }

            return BuildHash(blocks);
        }

        internal static byte[] BuildHash(Mat<float> blocks)
        {
            var hash = new byte[72];
            int hash_index;
            int nb_ones = 0, nb_zeros = 0;
            int bit_index = 0;
            byte hashbyte = 0;
            for (int rindex = 0; rindex < 31 - 2; rindex += 4)
            {
                for (int cindex = 0; cindex < 31 - 2; cindex += 4)
                {
                    Mat subsec = blocks.SubMat(rindex, rindex + 3, cindex, cindex + 3);
                    subsec = subsec.Clone();
                    subsec = subsec.Reshape(0, 1);
                    var ave = subsec.Mean().Val0;
                    var subsec_i = subsec.GetGenericIndexer<float>();
                    for (var I = 0; I < subsec.Width; I++)
                    {
                        hashbyte <<= 1;
                        if (subsec_i[0, I] > ave)
                        {
                            hashbyte |= 0x01;
                            nb_ones++;
                        }
                        else
                        {
                            nb_zeros++;
                        }
                        bit_index++;
                        if ((bit_index % 8) == 0)
                        {
                            hash_index = (int)(bit_index / 8) - 1;
                            hash[hash_index] = hashbyte;
                            hashbyte = 0x00;
                        }
                    }
                }
            }
            return hash;
        }

        public static double HammingDistance(byte[] hashA, byte[] hashB)
        {
            int lenA = hashA.Length;
            int lenB = hashB.Length;
            if (lenA != lenB)
            {
                return -1.0;
            }
            if ((hashA == null) || (hashB == null) || (lenA <= 0))
            {
                return -1.0;
            }
            double dist = 0;
            byte D = 0;
            for (int i = 0; i < lenA; i++)
            {
                D = (byte)(hashA[i] ^ hashB[i]);
                dist += CountBits(D);
            }
            double bits = (double)lenA * 8;
            return dist / bits;
        }

        internal static Mat<float> GetMHKernel(float alpha, float level)
        {
            int sigma = (int)(4 * MathF.Pow(alpha, level));
            float xpos, ypos, A;

            var size = 2 * sigma + 1;
            var pkernel = new Mat<float>(size, size, new Scalar(0));

            var indexer = pkernel.GetIndexer();
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    xpos = MathF.Pow(alpha, -level) * (x - sigma);
                    ypos = MathF.Pow((float)alpha, (float)-level) * (y - sigma);
                    A = xpos * xpos + ypos * ypos;
                    indexer[y, x] = (2 - A) * MathF.Exp(-A / 2);
                }
            }

            return pkernel;
        }

        internal static int CountBits(byte val)
        {
            int num = 0;
            while (val != 0)
            {
                num++;
                val &= (byte)(val - 1);
            }
            return num;
        }
    }
}
