﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenCvSharp;

[assembly: InternalsVisibleTo("Tests")]
namespace PHash
{
    public static class DCT
    {
        private static readonly Mat DCT_MATRIX_32 = CreateDCTMatrix(32);
        private static readonly Mat DCT_MATRIX_32_TRANSP = CreateDCTMatrix(32).Transpose();

        public static UInt64 GetImageHash(string file, int clampWidth, int clampHeight)
        {
            using var fs = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using var src = Mat.FromStream(fs, ImreadModes.Color);

            if (src == null)
                return 0;

            if (clampWidth > 0 && clampHeight > 0)
            {
                if (src.Width > clampWidth || src.Height > clampHeight)
                {
                    var clamp_asp = (double)clampWidth / clampHeight;
                    var inp_asp = (double)src.Width / src.Height;
                    var factor = inp_asp > clamp_asp 
                        ? (double)clampWidth / src.Width 
                        : (double)clampHeight / src.Height;
                    
                    Cv2.Resize(src, src, Size.Zero, factor, factor, InterpolationFlags.Linear);
                }
            }

            if (src.Width == 0)
                return 0;

            Mat img = null;
            if (src.Channels() >= 3)
            {
                using var chan0 = Utils.GetBrightnessComponent(src);
                img = new Mat(chan0.Size(), MatType.CV_32FC1);
                Cv2.BoxFilter(chan0, img, MatType.CV_32FC1, new Size(7, 7), null, normalize: false, BorderTypes.Replicate);
            }
            else if (src.Channels() == 1)
            {
                img = new Mat(src.Size(), MatType.CV_32FC1);
                Cv2.BoxFilter(src, img, MatType.CV_32FC1, new Size(7, 7), null, normalize: false, BorderTypes.Replicate);
            }
            src.Dispose();

            img = img.Resize(new Size(32, 32), 0, 0, InterpolationFlags.Nearest);
            Mat C = DCT_MATRIX_32;
            Mat Ctransp = DCT_MATRIX_32_TRANSP;
            using Mat dctImage = C * img * Ctransp;
            using Mat subsec = dctImage.SubMat(1, 9, 1, 9).Clone().Reshape(0, 1);

            img.Dispose();

            float median = GetMedianValue(subsec);
            UInt64 one = 0x0000000000000001;
            UInt64 hash = 0x0000000000000000;

            var subsec_i = subsec.GetGenericIndexer<float>();
            for (int i = 0; i < 64; i++)
            {
                float current = subsec_i[0, i];
                if (current > median) hash |= one;
                one <<= 1;
            }

            return hash;
        }

        private static float GetMedianValue(Mat m)
        {
            var s = m.Width;
            var idx = m.GetGenericIndexer<float>();
            var tmp = new List<float>(s);
            for (int i = 0; i < s; i++)
                tmp.Add(idx[0, i]);
            var res = Utils.NthOrderStatistic(tmp, s >> 1);
            return (s % 2 != 0) ? res : ((res + Utils.NthOrderStatistic(tmp, (s >> 1) - 1)) / 2f);
        }

        internal static Mat CreateDCTMatrix(int N) {
            var ptr_matrix = new Mat(N, N, MatType.CV_32FC1, new Scalar(1 / Math.Sqrt(N)));
            float c1 = (float)Math.Sqrt(2.0 / N);

            var indexer = ptr_matrix.GetGenericIndexer<float>();
            for (int x = 0; x<N; x++) {
                for (int y = 1; y<N; y++) {
                    indexer[y, x] = c1 * MathF.Cos((MathF.PI / 2 / N) * y * (2 * x + 1));
                }
            }
            return ptr_matrix;
        }

        public static int HammingDistance(UInt64 hash1, UInt64 hash2)
        {
            UInt64 x = hash1 ^ hash2;
            const UInt64 m1 = 0x5555555555555555UL;
            const UInt64 m2 = 0x3333333333333333UL;
            const UInt64 h01 = 0x0101010101010101UL;
            const UInt64 m4 = 0x0f0f0f0f0f0f0f0fUL;
            x -= (x >> 1) & m1;
            x = (x & m2) + ((x >> 2) & m2);
            x = (x + (x >> 4)) & m4;
            return (int)((x * h01) >> 56);
        }

        /*ulong64* ph_dct_videohash(const char* filename, int &Length);
        double ph_dct_videohash_dist(ulong64* hashA, int N1,
                                                   ulong64* hashB, int N2,
                                                   int threshold = 21);*/
    }
}
