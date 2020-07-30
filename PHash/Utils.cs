using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using OpenCvSharp;

namespace PHash
{
    public static class Utils
    {
        /// <summary>
        /// Partitions the given list around a pivot element such that all elements on left of pivot are <= pivot
        /// and the ones at thr right are > pivot. This method can be used for sorting, N-order statistics such as
        /// as median finding algorithms.
        /// Pivot is selected ranodmly if random number generator is supplied else its selected as last element in the list.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
        /// </summary>
        private static int Partition<T>(this IList<T> list, int start, int end, Random rnd = null) where T : IComparable<T>
        {
            if (rnd != null)
                list.Swap(end, rnd.Next(start, end + 1));

            var pivot = list[end];
            var lastLow = start - 1;
            for (var i = start; i < end; i++)
            {
                if (list[i].CompareTo(pivot) <= 0)
                    list.Swap(i, ++lastLow);
            }
            list.Swap(end, ++lastLow);
            return lastLow;
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, 
        /// n=1 returns 2nd smallest element etc.
        /// Note: specified list would be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        public static T NthOrderStatistic<T>(this IList<T> list, int n, Random rnd = null) where T : IComparable<T>
        {
            return NthOrderStatistic(list, n, 0, list.Count - 1, rnd);
        }

        private static T NthOrderStatistic<T>(this IList<T> list, int n, int start, int end, Random rnd) where T : IComparable<T>
        {
            while (true)
            {
                var pivotIndex = list.Partition(start, end, rnd);
                if (pivotIndex == n)
                    return list[pivotIndex];

                if (n < pivotIndex)
                    end = pivotIndex - 1;
                else
                    start = pivotIndex + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this IList<T> list, int i, int j)
        {
            if (i == j)   //This check is not required but Partition function may make many calls so its for perf reason
                return;
            var temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }

        /// <summary>
        /// Note: specified list would be mutated in the process.
        /// </summary>
        public static T Median<T>(this IList<T> list) where T : IComparable<T>
        {
            return list.NthOrderStatistic((list.Count - 1) / 2);
        }

        public static int Clamp(int val, int val_min, int val_max)
        {
            return val <= val_min
                ? val_min
                : val >= val_max
                    ? val_max
                    : val;
        }

        public static Mat GetBrightnessComponentCV(Mat src)
        {
            var conv = src.CvtColor(ColorConversionCodes.BGR2YCrCb);
            return conv.ExtractChannel(0);
        }

        /// <summary>
        /// original pHash method - RGB to YCbCr (16..235 range), returns Y component
        /// </summary>
        /// <param name="src">Input BGR U8 mat</param>
        /// <returns>output - brightness U8 mat</returns>
        public static Mat GetBrightnessComponent(Mat src)
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
                    res_i[y, x] = (byte)Utils.Clamp(Y, 0, 255);
                }
            }
            return result;
        }
    }
}
