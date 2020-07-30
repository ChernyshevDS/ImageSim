using System;
using System.Collections.Generic;
using System.Text;
using OpenCvSharp;

namespace PHash
{
    public static class Marr
    {
        //uint8_t* ph_mh_imagehash(const char* filename, int &N, float alpha = 2.0f, float lvl = 1.0f);
        public static byte[] GetImageHash(string filename, float alpha = 2.0f, float lvl = 1.0f)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            var hash = new byte[72];
            using var fs = new System.IO.FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using var src = Mat.FromStream(fs, ImreadModes.Color);

            Mat img = null;
            if (src.Channels() == 3)
            {
                /*img = tmp.blur(1.0)
                          .resize(512, 512, 1, 1, 5)
                          .get_equalize(256);*/

                var tmp = Utils.GetBrightnessComponent(src);
                Cv2.GaussianBlur(tmp, tmp, Size.Zero, 1.0, 1.0, BorderTypes.Replicate);
                tmp = tmp.Resize(new Size(512, 512), 0, 0, InterpolationFlags.Cubic);
                Cv2.EqualizeHist(tmp, img);
            }
            else
            {
                /*img = src.channel(0)
                          .blur(1.0)
                          .resize(512, 512, 1, 1, 5)
                          .get_equalize(256);*/
                var tmp = src.ExtractChannel(0);
                Cv2.GaussianBlur(tmp, tmp, Size.Zero, 1.0, 1.0, BorderTypes.Replicate);
                tmp = tmp.Resize(new Size(512, 512), 0, 0, InterpolationFlags.Cubic);
                Cv2.EqualizeHist(tmp, img);
            }
            //src.clear();

            //CImg<float>* pMHKernel = GetMHKernel(alpha, lvl);
            var pMHKernel = GetMHKernel(alpha, lvl);

            //CImg<float> fresp = img.get_correlate(*pMHKernel);
            Mat fresp = null;
            Cv2.Filter2D(img, fresp, MatType.CV_32FC1, pMHKernel, null, 0, BorderTypes.Replicate);

            //img.clear();
            //fresp.normalize(0, 1.0);
            fresp = fresp.Normalize();

            //CImg<float> blocks(31, 31, 1, 1, 0);
            Mat blocks = new Mat(31, 31, MatType.CV_32FC1, new Scalar(0));
            var blocks_i = blocks.GetGenericIndexer<float>();
            for (int rindex = 0; rindex < 31; rindex++)
            {
                for (int cindex = 0; cindex < 31; cindex++)
                {
                    /*blocks(rindex, cindex) =
                        fresp.get_crop(rindex * 16, cindex * 16, 
                            rindex * 16 + 16 - 1,
                            cindex * 16 + 16 - 1)
                            .sum();*/
                    var x0 = rindex * 16;
                    var y0 = cindex * 16;
                    var x1 = rindex * 16 + 16 - 1;
                    var y1 = cindex * 16 + 16 - 1;
                    blocks_i[cindex, rindex] = (float)fresp.SubMat(y0, y1 + 1, x0, x1 + 1).Sum().Val0;
                }
            }

            int hash_index;
            int nb_ones = 0, nb_zeros = 0;
            int bit_index = 0;
            byte hashbyte = 0;
            for (int rindex = 0; rindex < 31 - 2; rindex += 4)
            {
                //CImg<float> subsec;
                Mat subsec;
                for (int cindex = 0; cindex < 31 - 2; cindex += 4)
                {
                    //subsec = blocks.get_crop(cindex, rindex, cindex + 2, rindex + 2).unroll('x');
                    subsec = blocks.SubMat(rindex, cindex, rindex + 3, cindex + 3).Clone().Reshape(0, 1);
                    //float ave = subsec.mean();
                    var ave = (float)subsec.Mean().Val0;
                    //cimg_forX(subsec, I) {
                    var subsec_i = subsec.GetGenericIndexer<float>();
                    for(var I = 0; I < subsec.Height; I++) 
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

        //double ph_hammingdistance2(byte[] hashA, int lenA, byte[] hashB, int lenB)
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
                dist += ph_bitcount8(D);
            }
            double bits = (double)lenA * 8;
            return dist / bits;
        }

        //CImg<float>* GetMHKernel(float alpha, float level)
        internal static Mat GetMHKernel(float alpha, float level)
        {
            int sigma = (int)(4 * MathF.Pow(alpha, level));
            float xpos, ypos, A;

            var size = 2 * sigma + 1;
            //var pkernel = new CImg<float>(2 * sigma + 1, 2 * sigma + 1, 1, 1, 0);
            var pkernel = new Mat(size, size, MatType.CV_32FC1, new Scalar(0));

            /*cimg_forXY(*pkernel, X, Y) {
                xpos = pow((float)alpha, (float)-level) * (X - sigma);
                ypos = pow((float)alpha, (float)-level) * (Y - sigma);
                A = xpos * xpos + ypos * ypos;
                pkernel->atXY(X, Y) = (2 - A) * exp(-A / 2);
            }*/
            var indexer = pkernel.GetGenericIndexer<float>();
            for (int x = 0; x < size; x++)
            {
                for (int y = 1; y < size; y++)
                {
                    xpos = MathF.Pow(alpha, -level) * (x - sigma);
                    ypos = MathF.Pow((float)alpha, (float)-level) * (y - sigma);
                    A = xpos * xpos + ypos * ypos;
                    indexer[y, x] = (2 - A) * MathF.Exp(-A / 2);
                }
            }

            return pkernel;
        }

        private static int ph_bitcount8(byte val)
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
