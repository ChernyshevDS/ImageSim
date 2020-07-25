﻿using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace PHash
{
    public static class PHash
    {
        /*int ph_compare_images(const CImg<uint8_t> &imA,
                                            const CImg<uint8_t> &imB,
                                            double &pcc, double sigma = 3.5,
                                            double gamma = 1.0, int N = 180,
                                            double threshold = 0.90);*/

        private static void print_mat<T>(Mat img) where T : struct
        {
            for (int i = 0; i < img.Channels(); ++i)
            {
                System.Diagnostics.Debug.WriteLine("Channel {0}:", i);
                var channel = img.ExtractChannel(i).Reshape(0, 1);

                for (int px = 0; px < channel.Width; px++)
                {
                    var channel_i = channel.Get<T>(0, px);
                    System.Diagnostics.Debug.Write($"{channel_i} ");
                }
                /*var channel = img.ExtractChannel(i);
                for (int y = 0; y < channel.Height; y++)
                {
                    for (int x = 0; x < channel.Width; x++)
                    {
                        var channel_i = channel.Get<byte>(y, x);
                        System.Diagnostics.Debug.Write($"{channel_i},");
                    }
                }*/
            }
        }

        private static int Clamp(int val, int val_min, int val_max) {
            return val <= val_min 
                ? val_min 
                : val >= val_max
                    ? val_max 
                    : val;
        }

        private static Mat GetBrightnessComponentCV(Mat src)
        {
            var conv = src.CvtColor(ColorConversionCodes.BGR2YCrCb);
            return conv.ExtractChannel(0);
        }

        //input - BGR U8, output - Y U8
        private static Mat GetBrightnessComponent(Mat src)
        {
            var result = new Mat(src.Size(), MatType.CV_8UC1);
            var indexer = src.GetGenericIndexer<Vec3b>();
            var res_i = result.GetGenericIndexer<byte>();
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Vec3b color = indexer[y, x];    //BGR
                    var R = color.Item2;
                    var G = color.Item1;
                    var B = color.Item0;
                    var Y = (66 * R + 129 * G + 25 * B + 128) / 256 + 16;
                    res_i[y, x] = (byte)Clamp(Y, 0, 255);
                }
            }
            return result;
        }

        public static int ph_dct_imagehash(string file, ref UInt64 hash)
        {
            using var src = new Mat(file, ImreadModes.Color);

            Mat img = null;
            if (src.Channels() >= 3)
            {
                var chan0 = GetBrightnessComponent(src);
                img = new Mat(chan0.Size(), MatType.CV_32FC1);
                Cv2.BoxFilter(chan0, img, MatType.CV_32FC1, new Size(7, 7), null, normalize: false, BorderTypes.Replicate);
            }
            else if (img.Channels() == 1)
            {
                img = new Mat(src.Size(), MatType.CV_32FC1);
                Cv2.BoxFilter(src, img, MatType.CV_32FC1, new Size(7, 7), null, normalize: false, BorderTypes.Replicate);
            }

            img = img.Resize(new Size(32, 32), 0, 0, InterpolationFlags.Nearest);
            
            using Mat C = ph_dct_matrix(32);
        
            var Ctransp = C.Clone().Transpose();
        
            Mat dctImage = C * img * Ctransp;
            
            Mat tmp = dctImage.SubMat(1, 9, 1, 9).Clone();
            
            Mat subsec = tmp.Reshape(0, 1);
            print_mat<float>(subsec);

            float median = get_median(subsec);
            UInt64 one = 0x0000000000000001;
            hash = 0x0000000000000000;

            var subsec_i = subsec.GetGenericIndexer<float>();
            for (int i = 0; i < 64; i++)
            {
                float current = subsec_i[0, i];
                if (current > median) hash |= one;
                one <<= 1;
            }

            return 0;
        }

        private static float get_median(Mat m)
        {
            var s = m.Width;
            var idx = m.GetGenericIndexer<float>();
            var tmp = new List<float>(s);
            for (int i = 0; i < s; i++)
                tmp.Add(idx[0, i]);
            var res = Utils.NthOrderStatistic(tmp, s >> 1);
            return (s % 2 != 0) ? res : ((res + Utils.NthOrderStatistic(tmp, (s >> 1) - 1)) / 2f);
        }

        public static Mat ph_dct_matrix(int N) {
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

        public static int ph_hamming_distance(UInt64 hash1, UInt64 hash2)
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

        /*byte[] ph_mh_imagehash(string filename, ref int N,
                                               float alpha = 2.0f,
                                               float lvl = 1.0f)
        { 
        }*/

        //double ph_hammingdistance2(byte[] hashA, int lenA, byte[] hashB, int lenB)
        public static double ph_hammingdistance2(byte[] hashA, byte[] hashB)
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
                dist += ph_bitcount8(D);
            }
            double bits = (double)lenA * 8;
            return dist / bits;
        }

        public static int ph_bitcount8(byte val)
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
