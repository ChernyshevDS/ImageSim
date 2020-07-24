using System;
using OpenCvSharp;

namespace PHash
{
    public class PHash
    {
        /*int ph_compare_images(const CImg<uint8_t> &imA,
                                            const CImg<uint8_t> &imB,
                                            double &pcc, double sigma = 3.5,
                                            double gamma = 1.0, int N = 180,
                                            double threshold = 0.90);*/

        int ph_dct_imagehash(string file, ref UInt64 hash)
        {
            if (string.IsNullOrEmpty(file))
            {
                return -1;
            }

            using var src = new Mat(file, ImreadModes.Color);
            /*CImg<uint8_t> src;
            try
            {
                src.load(file);
            }
            catch (CImgIOException ex)
            {
                return -1;
            }*/

            var meanfilter = new Mat(7, 7, MatType.CV_32FC1, new Scalar(1.0f));
            
            /*\param size_x Image width().
              \param size_y Image height().
              \param size_z Image depth().
              \param size_c Image spectrum() (number of channels).
              \param value Initialization value.*/
            //CImg<float> meanfilter(7, 7, 1, 1, 1);
            //CImg<float> img;
            
            Mat img = null;
            if (src.Channels() >= 3)
            {
                var chan0 = src.CvtColor(ColorConversionCodes.BGR2YCrCb).ExtractChannel(0);
                img = chan0.Filter2D(MatType.FromInt32(-1), meanfilter);
                //img = src.RGBtoYCbCr().channel(0).get_convolve(meanfilter);
            }
            else if (img.Channels() == 1)
            {
                //img = src.get_convolve(meanfilter);
                img = src.Filter2D(MatType.FromInt32(-1), meanfilter);
            }

            img.Resize(new Size(32, 32));

            using Mat C = ph_dct_matrix(32);
            //CImg<float> Ctransp = C->get_transpose();
            var Ctransp = C.Clone().Transpose();

            //CImg<float> dctImage = (*C) * img * Ctransp;
            Mat dctImage = C * img * Ctransp;

            //CImg<float> subsec = dctImage.crop(1, 1, 8, 8).unroll('x');
            Mat subsec = dctImage.SubMat(1, 8, 1, 8).Reshape(1, 1);
            

            float median = subsec.median();
            UInt64 one = 0x0000000000000001;
            hash = 0x0000000000000000;
            for (int i = 0; i < 64; i++)
            {
                float current = subsec(i);
                if (current > median) hash |= one;
                one = one << 1;
            }


            //delete C;

            return 0;
        }

        double medianMat(Mat Input, int nVals)
        {
            // COMPUTE HISTOGRAM OF SINGLE CHANNEL MATRIX
            float range[] = { 0, nVals };
            const float* histRange = { range };
            bool uniform = true; bool accumulate = false;
            
            Cv2.CalcHist(new Mat[] { Input }, new int[] { 0 }, null, )

            cv::Mat hist;

/*const Mat* images,
int nimages,
const int* channels,
InputArray  mask,
OutputArray hist,
int dims,
const int* histSize,
const float** ranges,
bool uniform = true,
bool accumulate = false*/

            calcHist(&Input, 1, 0, cv::Mat(), hist, 1, &nVals, &histRange, uniform, accumulate);

            // COMPUTE CUMULATIVE DISTRIBUTION FUNCTION (CDF)
            cv::Mat cdf;
            hist.copyTo(cdf);
            for (int i = 1; i <= nVals - 1; i++)
            {
                cdf.at<float>(i) += cdf.at<float>(i - 1);
            }
            cdf /= Input.total();

            // COMPUTE MEDIAN
            double medianVal;
            for (int i = 0; i <= nVals - 1; i++)
            {
                if (cdf.at<float>(i) >= 0.5) { medianVal = i; break; }
            }
            return medianVal / nVals;
        }

        Mat ph_dct_matrix(int N) {
            //CImg<float>* ptr_matrix = new CImg<float>(N, N, 1, 1, 1 / sqrt((float)N));
            var ptr_matrix = new Mat(N, N, MatType.CV_32FC1, new Scalar(1 / Math.Sqrt(N)));
            float c1 = (float)Math.Sqrt(2.0 / N);

            /*for (int x = 0; x<N; x++) {
                for (int y = 1; y<N; y++) {
                    *ptr_matrix->data(x, y) = c1 * (float) cos((MathF.PI / 2 / N) * y* (2 * x + 1));
                }
            }*/
            var indexer = ptr_matrix.GetGenericIndexer<float>();
            for (int x = 0; x<N; x++) {
                for (int y = 1; y<N; y++) {
                    indexer[x, y] = c1 * MathF.Cos((MathF.PI / 2 / N) * y * (2 * x + 1));
                }
            }
            return ptr_matrix;
        }

        int ph_hamming_distance(UInt64 hash1, UInt64 hash2)
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
        double ph_hammingdistance2(byte[] hashA, byte[] hashB)
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

        int ph_bitcount8(byte val)
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
