using System;
using System.Collections.Generic;
using System.IO;
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

    namespace CV
    {
        public interface IHashingAlgorithm<TFeature>
        {
            string Name { get; }
            TFeature GetDescriptor(string path);
            double GetSimilarity(TFeature left, TFeature right);
        }

        public interface ISerializableDescriptor
        {
            void Serialize(Stream stream);
            void Deserialize(Stream stream);
        }

        internal static class Utils
        {
            public static Mat LoadImage(string filepath)
            {
                using var fs = new System.IO.FileStream(filepath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                return Mat.FromStream(fs, ImreadModes.Color);
            }
        }

        public class DCT : IHashingAlgorithm<DCT.Descriptor>
        {
            private readonly OpenCvSharp.ImgHash.PHash impl;

            public string Name => "DCTImageHash";

            public class Descriptor : OpenCVMatDescriptor
            {
                internal Descriptor(Mat mat) : base(mat) { }
                public Descriptor() : base(null) { }
            }

            public DCT()
            {
                impl = OpenCvSharp.ImgHash.PHash.Create();
            }

            public Descriptor GetDescriptor(string path)
            {
                using var mat = Utils.LoadImage(path);
                var ret = new Mat();
                impl.Compute(mat, ret);
                return new Descriptor(ret);
            }

            public double GetSimilarity(Descriptor hash1, Descriptor hash2)
            {
                const double max_distance = 64;
                var distance = impl.Compare(hash1.Data, hash2.Data);
                return 1.0 - (distance / max_distance);
            }
        }

        public class Marr : IHashingAlgorithm<Marr.Descriptor>
        {
            public string Name => "MarrImageHash";

            private readonly OpenCvSharp.ImgHash.MarrHildrethHash impl;

            public Marr(float alpha = 2.0f, float scale = 1.0f)
            {
                impl = OpenCvSharp.ImgHash.MarrHildrethHash.Create(alpha, scale);
            }

            public void SetParameters(float alpha = 2.0f, float scale = 1.0f) 
                => impl.SetKernelParam(alpha, scale);

            public Descriptor GetDescriptor(string path)
            {
                using var mat = Utils.LoadImage(path);
                var ret = new Mat();
                impl.Compute(mat, ret);
                return new Descriptor(ret);
            }

            public double GetSimilarity(Descriptor left, Descriptor right)
            {
                const double max_distance = 576;
                var distance = impl.Compare(left.Data, right.Data);
                return 1.0 - (distance / max_distance);
            }

            public class Descriptor : OpenCVMatDescriptor
            {
                internal Descriptor(Mat mat) : base(mat) { }
                public Descriptor() : base(null) { }
            }
        }

        public class Radial : IHashingAlgorithm<Radial.Descriptor>
        {
            public string Name => "RadialImageHash";

            private readonly OpenCvSharp.ImgHash.RadialVarianceHash impl;

            public double Sigma { get => impl.Sigma; set => impl.Sigma = value; }
            public int NLines { get => impl.NumOfAngleLine; set => impl.NumOfAngleLine = value; }

            public Radial(double sigma = 1.0, int nLines = 180)
            {
                impl = OpenCvSharp.ImgHash.RadialVarianceHash.Create(sigma, nLines);
            }

            public Descriptor GetDescriptor(string path)
            {
                using var mat = Utils.LoadImage(path);
                var ret = new Mat();
                impl.Compute(mat, ret);
                return new Descriptor(ret);
            }

            public double GetSimilarity(Descriptor left, Descriptor right)
            {
                return impl.Compare(left.Data, right.Data);
            }

            public class Descriptor : OpenCVMatDescriptor
            {
                internal Descriptor(Mat mat) : base(mat) { }
                public Descriptor() : base(null) { }
            }
        }

        public class ColorMoment : IHashingAlgorithm<ColorMoment.Descriptor>
        {
            public string Name => "ColorMomentImageHash";

            private readonly OpenCvSharp.ImgHash.ColorMomentHash impl 
                = OpenCvSharp.ImgHash.ColorMomentHash.Create();

            public Descriptor GetDescriptor(string path)
            {
                using var mat = Utils.LoadImage(path);
                var ret = new Mat();
                impl.Compute(mat, ret);
                return new Descriptor(ret);
            }

            public double GetSimilarity(Descriptor left, Descriptor right)
            {
                //FIXME returns distance instead of similarity
                return impl.Compare(left.Data, right.Data);
            }

            public class Descriptor : OpenCVMatDescriptor
            {
                internal Descriptor(Mat mat) : base(mat) { }
                public Descriptor() : base(null) { }
            }
        }

        public class BlockMean : IHashingAlgorithm<BlockMean.Descriptor>
        {
            public string Name => "BlockMeanImageHash";

            private readonly OpenCvSharp.ImgHash.BlockMeanHash impl
                = OpenCvSharp.ImgHash.BlockMeanHash.Create(OpenCvSharp.ImgHash.BlockMeanHashMode.Mode0);

            public Descriptor GetDescriptor(string path)
            {
                using var mat = Utils.LoadImage(path);
                var ret = new Mat();
                impl.Compute(mat, ret);
                return new Descriptor(ret);
            }

            public double GetSimilarity(Descriptor left, Descriptor right)
            {
                const double max_distance = 256;
                var distance = impl.Compare(left.Data, right.Data);
                return 1.0 - (distance / max_distance);
            }

            public class Descriptor : OpenCVMatDescriptor
            {
                internal Descriptor(Mat mat) : base(mat) { }
                public Descriptor() : base(null) { }
            }
        }

        public abstract class OpenCVMatDescriptor : ISerializableDescriptor
        {
            internal Mat Data { get; private set; }

            protected OpenCVMatDescriptor(Mat data)
            {
                Data = data;
            }

            public virtual void Serialize(Stream stream)
            {
                Data.WriteToStream(stream, ".pgm", new ImageEncodingParam(ImwriteFlags.PxmBinary, 1));
            }

            public virtual void Deserialize(Stream stream)
            {
                if (Data != null)
                    Data.Dispose();

                Data = Mat.FromStream(stream, ImreadModes.Unchanged);
            }
        }
    }
}
