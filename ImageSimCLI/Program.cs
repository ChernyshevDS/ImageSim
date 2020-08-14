using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using PHash.CV;

namespace ImageSimCLI
{
    public class Options
    {
        [Option('f', "fullpaths", Default = false, HelpText = "Print full file paths")]
        public bool FullPath { get; set; }

        [Option('v', "vertical", Required = false, Separator = ':', HelpText = "First set of files to be compared")]
        public IEnumerable<string> VerticalSet { get; set; }
        
        [Option('h', "horizontal", Required = false, Separator = ':', HelpText = "Second set of files to be compared")]
        public IEnumerable<string> HorizontalSet { get; set; }

        [Option('e', "exclude", Required = false, Separator = ':', HelpText = "Paths, excluded from comparison")]
        public IEnumerable<string> Excluded { get; set; }

        [Value(0, MetaName = "files", Required = false, HelpText = "Files to be compared")]
        public IEnumerable<string> Values { get; set; }
    }

    [Verb("dct", HelpText = "Discrete cosine transform (DCT) image hash")]
    public class DCTOptions : Options { }

    [Verb("colormoment", HelpText = "Image hash based on color moments")]
    public class ColorMomentOptions : Options { }

    [Verb("blockmean", HelpText = "Block mean image hash")]
    public class BlockMeanOptions : Options { }

    [Verb("marr", HelpText = "Marr-Hildreth wavelet image hash")]
    public class MarrOptions : Options
    { 
        [Option("alpha", Default = 2.0, HelpText = "Scale factor for Marr wavelet")]
        public double Alpha { get; set; }
        [Option("scale", Default = 1.0, HelpText = "Level of scale factor")]
        public double Scale { get; set; }
    }

    [Verb("radial", HelpText = "Image hash based on Radon transform")]
    public class RadialOptions : Options
    {
        [Option("sigma", Default = 1.0, HelpText = "Gaussian kernel standard deviation")]
        public double Sigma { get; set; }
        [Option("nlines", Default = 180, HelpText = "The number of angles to consider")]
        public int NLines { get; set; }
    }

    interface ISimilarityAlgorithm
    {
        string Name { get; }
        double GetSimilarity(string left, string right);
    }

    static class Algoritms
    {
        public static CachingSimilarityAlgorithm<TFeature> WithCache<TFeature>(this IHashingAlgorithm<TFeature> hashingAlgorithm)
        {
            return new CachingSimilarityAlgorithm<TFeature>(hashingAlgorithm);
        }

        public static ISimilarityAlgorithm CreateFromOptions(DCTOptions opt) => WithCache(new DCT());
        public static ISimilarityAlgorithm CreateFromOptions(MarrOptions opt) => WithCache(new Marr((float)opt.Alpha, (float)opt.Scale));
        public static ISimilarityAlgorithm CreateFromOptions(RadialOptions opt) => WithCache(new Radial(opt.Sigma, opt.NLines));
        public static ISimilarityAlgorithm CreateFromOptions(ColorMomentOptions opt) => WithCache(new ColorMoment());
        public static ISimilarityAlgorithm CreateFromOptions(BlockMeanOptions opt) => WithCache(new BlockMean());
    }

    class CachingSimilarityAlgorithm<TFeature> : ISimilarityAlgorithm
    {
        public string Name => HashingAlgorithm.Name;

        private readonly Dictionary<string, TFeature> featureCache = new Dictionary<string, TFeature>();
        
        public IHashingAlgorithm<TFeature> HashingAlgorithm { get; private set; }

        public CachingSimilarityAlgorithm(IHashingAlgorithm<TFeature> hashingAlgorithm)
        {
            HashingAlgorithm = hashingAlgorithm;
        }

        private TFeature GetFeature(string path)
        {
            if (featureCache.TryGetValue(path, out TFeature feature))
            {
                return feature;
            }
            else 
            {
                var newFeature = HashingAlgorithm.GetDescriptor(path);
                featureCache.Add(path, newFeature);
                return newFeature;
            }
        }

        public double GetSimilarity(string left, string right)
        {
            var lfeat = GetFeature(left);
            var rfeat = GetFeature(right);
            return HashingAlgorithm.GetSimilarity(lfeat, rfeat);
        }
    }

    class Program
    {
        private static void RunWithAlgorithm(ISimilarityAlgorithm algorithm, Options opt)
        {
            /*Console.WriteLine("Name: " + algorithm.Name);
            Console.WriteLine("Vertical set:");
            foreach (var item in opt.VerticalSet ?? Enumerable.Empty<string>())
                Console.WriteLine("  " + item);
            Console.WriteLine("Horizontal set:");
            foreach (var item in opt.HorizontalSet ?? Enumerable.Empty<string>())
                Console.WriteLine("  " + item);
            Console.WriteLine("Excluded:");
            foreach (var item in opt.Excluded ?? Enumerable.Empty<string>())
                Console.WriteLine("  " + item);
            Console.WriteLine("Values:");
            foreach (var item in opt.Values ?? Enumerable.Empty<string>())
                Console.WriteLine("  " + item);*/
            FillNullOptions(opt);

            IEnumerable<string> vset = null, hset = null;

            var hasVer = opt.VerticalSet.Any();
            var hasHor = opt.HorizontalSet.Any();
            var hasOth = opt.Values.Any();
            //hasVer hasHor hasOth
            // 0 0 0
            if (!hasVer && !hasHor && !hasOth)
            {
                ExitWithError("Not enough files to run");
            } 
            // 0 0 1
            else if (!hasVer && !hasHor && hasOth)
            {
                vset = opt.Values;
                hset = opt.Values;
            } 
            // 0 1 0
            else if (!hasVer && hasHor && !hasOth)
            {
                vset = opt.HorizontalSet;
                hset = opt.HorizontalSet;
            } 
            // 0 1 1
            else if (!hasVer && hasHor && hasOth)
            {
                vset = opt.Values;
                hset = opt.HorizontalSet;
            } 
            // 1 0 0
            else if (hasVer && !hasHor && !hasOth)
            {
                vset = opt.VerticalSet;
                hset = opt.VerticalSet;
            } 
            // 1 0 1
            else if (hasVer && !hasHor && hasOth)
            {
                vset = opt.VerticalSet;
                hset = opt.Values;
            } 
            // 1 1 0
            else if (hasVer && hasHor && !hasOth)
            {
                vset = opt.VerticalSet;
                hset = opt.HorizontalSet;
            }
            // 1 1 1
            else
            {
                ExitWithError("Invalid options: if --vertical and --horizontal are set, no more values should be provided");
            }

            var excludedPaths = opt.Excluded.Select(x => Path.GetFullPath(x)).ToList();
            var vset_abs = vset.Select(x => Path.GetFullPath(x)).ToList();
            var hset_abs = hset.Select(x => Path.GetFullPath(x)).ToList();

            var vpaths = new List<string>();
            var hpaths = new List<string>();
            foreach (var item in vset_abs)
                PopulateFiles(item, vpaths, excludedPaths);
            foreach (var item in hset_abs)
                PopulateFiles(item, hpaths, excludedPaths);

            //print header
            var headers = opt.FullPath 
                ? hpaths
                : hpaths.Select(x => Path.GetFileName(x));
            var header = ";" + string.Join(";", headers);
            Console.WriteLine(header);

            foreach (var left in vpaths)
            {
                var fname = opt.FullPath ? left : Path.GetFileName(left);
                Console.Write(fname + ";");
                foreach (var right in hpaths)
                {
                    var dist = algorithm.GetSimilarity(left, right);
                    Console.Write($"{dist};");
                }
                Console.WriteLine();
            }

            Environment.Exit(0);
        }

        private static void ExitWithError(string msg)
        {
            Console.WriteLine(msg);
            Environment.Exit(1);
        }

        private static void FillNullOptions(Options opt)
        {
            if (opt.VerticalSet == null)
                opt.VerticalSet = Enumerable.Empty<string>();
            if (opt.HorizontalSet == null)
                opt.HorizontalSet = Enumerable.Empty<string>();
            if (opt.Excluded == null)
                opt.Excluded = Enumerable.Empty<string>();
            if (opt.Values == null)
                opt.Values = Enumerable.Empty<string>();
        }

        private static void PopulateFiles(string path, IList<string> result, IEnumerable<string> excluded)
        {
            if (excluded.Any(x => path.StartsWith(x)))
                return;

            if (File.Exists(path))
            {
                result.Add(path);
            }
            else if (Directory.Exists(path))
            {
                var entries = Directory.EnumerateFileSystemEntries(path);
                foreach (var entry in entries)
                {
                    PopulateFiles(entry, result, excluded);
                }
            }
        }

        private static void PrintErrorsAndExit(IEnumerable<Error> err)
        {
            if (err.Count() == 1)
            {
                var error = err.First();
                switch (error.Tag)
                {

                    case ErrorType.HelpRequestedError:
                    case ErrorType.HelpVerbRequestedError:
                    case ErrorType.VersionRequestedError:
                        //not an error
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Error: " + error.Tag.ToString());
                        break;
                }
            }
            else 
            {
                Console.WriteLine("Errors:");
                foreach (var item in err)
                {
                    Console.WriteLine("  " + item.Tag.ToString());
                }
            }
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<DCTOptions, MarrOptions, RadialOptions, ColorMomentOptions, BlockMeanOptions>(args)
                .WithParsed<DCTOptions>(x => RunWithAlgorithm(Algoritms.CreateFromOptions(x), x))
                .WithParsed<MarrOptions>(x => RunWithAlgorithm(Algoritms.CreateFromOptions(x), x))
                .WithParsed<RadialOptions>(x => RunWithAlgorithm(Algoritms.CreateFromOptions(x), x))
                .WithParsed<ColorMomentOptions>(x => RunWithAlgorithm(Algoritms.CreateFromOptions(x), x))
                .WithParsed<BlockMeanOptions>(x => RunWithAlgorithm(Algoritms.CreateFromOptions(x), x))
                .WithNotParsed(err => PrintErrorsAndExit(err));
        }    
    }
}
