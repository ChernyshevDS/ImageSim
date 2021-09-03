using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenCvSharp;

namespace PHash
{
    public static class Radial
    {
        internal const double SQRT_TWO = 1.4142135623730950488016887242097;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ROUNDING_FACTOR(double x) => (((x) >= 0) ? 0.5 : -0.5);

        //int ph_compare_images(const char* file1, const char* file2, double &pcc,
        //                      double sigma, double gamma, int N, double threshold) 
        public static bool CompareImages(string file1, string file2, out double pcc,
                                        double sigma = 3.5, double gamma = 1.0, int N = 180,
                                        double threshold = 0.90)
        {
            using var imA = LoadMat(file1);
            var digestA = ph_image_digest(imA, sigma, gamma, N);
            using var imB = LoadMat(file2);
            var digestB = ph_image_digest(imB, sigma, gamma, N);

            pcc = ph_crosscorr(digestA, digestB);
            return (pcc > threshold);
        }
       
        internal static Mat LoadMat(string path)
        {
            using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var src = Mat.FromStream(fs, ImreadModes.Color);
            return src;
        }

        //int ph_image_digest(const CImg<uint8_t> &img, double sigma, double gamma,
        //                    Digest &digest, int N) 
        internal static Digest ph_image_digest(Mat img, double sigma, double gamma, int N) 
        {
            //CImg<uint8_t> graysc;
            Mat graysc;
            if (img.Channels() >= 3) {
                //graysc = img.get_RGBtoYCbCr().channel(0);
                graysc = Utils.GetBrightnessComponent(img);
            } else if (img.Channels() == 1) {
                graysc = img;
            } else {
                throw new ArgumentException("Image should have 1 or 3 channels", nameof(img));
            }

            //graysc.blur((float) sigma);
            graysc = graysc.GaussianBlur(Size.Zero, sigma, sigma, BorderTypes.Replicate);

            //(graysc / graysc.max()).pow(gamma);
#warning Original code doesn't make use of gamma? Double check.
            /*            
            graysc.MinMaxLoc(out double _, out double max_val);
            graysc /= max_val;
            Cv2.Pow(graysc, gamma, graysc);
            */
            var projs = ph_radon_projections(graysc, N);
            var features = ph_feature_vector(projs);
            var digest = ph_dct(features);
            return digest;
        }

        //int ph_crosscorr(const Digest &x, const Digest &y,
        //                 double &pcc, double threshold) 
        internal static double ph_crosscorr(Digest x, Digest y)
        {
            int N = y.coeffs.Length;
            var x_coeffs = x.coeffs;
            var y_coeffs = y.coeffs;

            var r = new double[N];

            double sumx = 0.0;
            double sumy = 0.0;
            for (int i = 0; i<N; i++) 
            {
                sumx += x_coeffs[i];
                sumy += y_coeffs[i];
            }
            double meanx = sumx / N;
            double meany = sumy / N;
            double max = 0;
            for (int d = 0; d < N; d++) 
            {
                double num = 0.0;
                double denx = 0.0;
                double deny = 0.0;
                for (int i = 0; i < N; i++) 
                {
                    num += (x_coeffs[i] - meanx) * (y_coeffs[(N + i - d) % N] - meany);
                    denx += Math.Pow((x_coeffs[i] - meanx), 2);
                    deny += Math.Pow((y_coeffs[(N + i - d) % N] - meany), 2);
                }
                r[d] = num / Math.Sqrt(denx* deny);
                if (r[d] > max) 
                    max = r[d];
            }
            //delete[] r;
            return max;
        }

        internal static Projections ph_radon_projections(Mat img, int N) 
        {
            Projections projs = new Projections();
            int width = img.Width;
            int height = img.Height;
            int D = (width > height) ? width : height;
            float x_center = (float)width / 2;
            float y_center = (float)height / 2;
            int x_off = (int)Math.Floor(x_center + ROUNDING_FACTOR(x_center));
            int y_off = (int)Math.Floor(y_center + ROUNDING_FACTOR(y_center));

            //projs.R = new CImg<uint8_t>(N, D, 1, 1, 0);
            projs.R = new Mat<byte>(D, N, Scalar.Black);
            //projs.nb_pix_perline = (int*) calloc(N, sizeof(int));
            projs.nb_pix_perline = new int[N];

            //if (!projs.R || !projs.nb_pix_perline) return EXIT_FAILURE;
            //projs.size = N;

            var ptr_radon_map = projs.R;
            var nb_per_line = projs.nb_pix_perline;

            var radon_map_i = ptr_radon_map.GetIndexer();
            var img_i = img.GetGenericIndexer<byte>();

            for (int k = 0; k < (N / 4 + 1); k++)
            {
                double theta = k * Math.PI / N;
                double alpha = Math.Tan(theta);
                for (int x = 0; x<D; x++) 
                {
                    double y = alpha * (x - x_off);
                    int yd = (int)Math.Floor(y + ROUNDING_FACTOR(y));
                    if ((yd + y_off >= 0) && (yd + y_off < height) && (x < width)) 
                    {
                        //*ptr_radon_map->data(k, x) = img(x, yd + y_off);
                        radon_map_i[x, k] = img_i[yd + y_off, x];
                        nb_per_line[k] += 1;
                    }
                    if ((yd + x_off >= 0) && (yd + x_off < width) && (k != N / 4) && (x < height)) 
                    {
                        //*ptr_radon_map->data(N / 2 - k, x) = img(yd + x_off, x);
                        radon_map_i[x, N / 2 - k] = img_i[x, yd + x_off];
                        nb_per_line[N / 2 - k] += 1;
                    }
                }
            }
            int j = 0;
            for (int k = 3 * N / 4; k<N; k++) 
            {
                double theta = k * Math.PI / N;
                double alpha = Math.Tan(theta);
                for (int x = 0; x<D; x++) 
                {
                    double y = alpha * (x - x_off);
                    int yd = (int)Math.Floor(y + ROUNDING_FACTOR(y));
                    if ((yd + y_off >= 0) && (yd + y_off<height) && (x<width)) {
                        //*ptr_radon_map->data(k, x) = img(x, yd + y_off);
                        radon_map_i[x, k] = img_i[yd + y_off, x];
                        nb_per_line[k] += 1;
                    }
                    if ((y_off - yd >= 0) && (y_off - yd<width) 
                        && (2 * y_off - x >= 0) && (2 * y_off - x<height) 
                        && (k != 3 * N / 4)) 
                    {
                        //*ptr_radon_map->data(k - j, x) = img(-yd + y_off, -(x - y_off) + y_off);
                        radon_map_i[x, k - j] = img_i[-(x - y_off) + y_off, -yd + y_off];
                        nb_per_line[k - j] += 1;
                    }
                }
                j += 2;
            }

            return projs;
        }

        internal static Features ph_feature_vector(in Projections projs) 
        {
            var projection_map = projs.R;
            var nb_perline = projs.nb_pix_perline;
            int N = projs.nb_pix_perline.Length;
            int D = projection_map.Height;

            var fv = new Features();
            fv.features = new double[N]; //(double*) malloc(N* sizeof(double));
            //fv.size = N;
            //if (!fv.features) return EXIT_FAILURE;

            var feat_v = fv.features;
            double sum = 0.0;
            double sum_sqd = 0.0;
            var projection_map_i = projection_map.GetIndexer();
            for (int k = 0; k<N; k++) 
            {
                double line_sum = 0.0;
                double line_sum_sqd = 0.0;
                int nb_pixels = nb_perline[k];
                for (int i = 0; i<D; i++) 
                {
                    //line_sum += projection_map(k, i);
                    //line_sum_sqd += projection_map(k, i) * projection_map(k, i);
                    line_sum += projection_map_i[i, k];
                    line_sum_sqd += projection_map_i[i, k] * projection_map_i[i, k];
                }
                feat_v[k] = (line_sum_sqd / nb_pixels) -
                                (line_sum * line_sum) / (nb_pixels * nb_pixels);
                sum += feat_v[k];
                sum_sqd += feat_v[k] * feat_v[k];
            }
            double mean = sum / N;
            double var = Math.Sqrt((sum_sqd / N) - (sum * sum) / (N * N));

            for (int i = 0; i<N; i++) {
                feat_v[i] = (feat_v[i] - mean) / var;
            }

            return fv;
        }

        internal static Digest ph_dct(in Features fv) 
        {
            int N = fv.features.Length;
            const int nb_coeffs = 40;

            var digest = new Digest();
            digest.coeffs = new byte[nb_coeffs]; // (uint8_t*) malloc(nb_coeffs* sizeof(uint8_t));
            //if (!digest.coeffs) 
            //    return EXIT_FAILURE;
            //digest.size = nb_coeffs;

            var R = fv.features;
            var D = digest.coeffs;

            var D_temp = new double[nb_coeffs];
            double max = 0.0;
            double min = 0.0;
            for (int k = 0; k<nb_coeffs; k++)
            {
                double sum = 0.0;
                for (int n = 0; n<N; n++) 
                {
                    double temp = R[n] * Math.Cos((Math.PI * (2 * n + 1) * k) / (2 * N));
                    sum += temp;
                }
                if (k == 0)
                    D_temp[k] = sum / Math.Sqrt((double) N);
                else
                    D_temp[k] = sum * SQRT_TWO / Math.Sqrt((double) N);
                if (D_temp[k] > max) 
                    max = D_temp[k];
                if (D_temp[k] < min) 
                    min = D_temp[k];
            }

            for (int i = 0; i<nb_coeffs; i++) {
                D[i] = (byte)(byte.MaxValue * (D_temp[i] - min) / (max - min));
            }

            return digest;
        }

        internal struct Digest
        {
            public byte[] coeffs;  // the head of the digest integer coefficient array
        }

        internal struct Features
        {
            public double[] features;  // the head of the feature array of double's
        }

        internal struct Projections
        {
            public Mat<byte> R;  // contains projections of image of angled lines through center
            public int[] nb_pix_perline;  // the head of int array denoting the number of pixels of each line
        }
    }
}
