using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ImageSimCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: imagesimcli <comparison type (inter, intra)> <folder of file 1 .. N>");
            }

            var compare_type = args[0];
            if (compare_type.Equals("inter", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine("Enumerating files...");
                var paths = new List<string>();
                foreach (var path in args.Skip(1))
                    PopulateFiles(path, paths);
                if (paths.Count < 2)
                {
                    Console.WriteLine("Error: at least 2 files needed. Aborting.");
                    return;
                }
                Console.WriteLine($"{paths.Count} files found");

                Console.Write($"Calculating hashes: 0 of {paths.Count}");
                var hashes = new Dictionary<string, ulong>(paths.Count);
                var index = 0;
                foreach (var file in paths)
                {
                    var hash = PHash.DCT.GetImageHash(file);
                    hashes[file] = hash;
                    Console.CursorLeft = 0;
                    Console.Write($"Calculating hashes: {++index} of {paths.Count}");
                }
                Console.WriteLine("\nDone");

                /*Console.WriteLine("Running tests...");
                var hashList = hashes.ToList();
                index = 0;
                var results = RunInterTests(hashList, (kv1, kv2) => 
                {
                    var distance = PHash.DCT.HammingDistance(kv1.Value, kv2.Value);
                    return new { Index = index++, Left = kv1.Key, Right = kv2.Key, Distance = distance };
                }, selfTest: true);

                Console.WriteLine("Done\nResults:");
                foreach (var item in results)
                {
                    Console.WriteLine($"{item.Index}; {item.Left}; {item.Right}; {item.Distance}");
                }*/
                var headers = paths.Select(x => Path.GetFileNameWithoutExtension(x));
                var header = ";" + string.Join(";", headers);
                Console.WriteLine(header);

                foreach (var left in paths)
                {
                    Console.Write(Path.GetFileNameWithoutExtension(left) + ";");
                    foreach (var right in paths)
                    {
                        var dist = PHash.DCT.HammingDistance(hashes[left], hashes[right]);
                        Console.Write($"{dist};");
                    }
                    Console.WriteLine();
                }
            }
            else if (compare_type.Equals("intra", StringComparison.InvariantCultureIgnoreCase))
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: imagesimcli intra <folder of file 1> <folder of file 2>");
                }
                var files1 = new List<string>();
                var files2 = new List<string>();
                PopulateFiles(args[1], files1);
                PopulateFiles(args[2], files2);

                files1.Sort();
                files2.Sort();

                var total = files1.Count + files2.Count;
                Console.Write($"Calculating hashes: 0 of {total}");
                var hashes = new Dictionary<string, ulong>(total);
                var index = 0;
                foreach (var file in files1.Concat(files2))
                {
                    var hash = PHash.DCT.GetImageHash(file);
                    hashes[file] = hash;
                    Console.CursorLeft = 0;
                    Console.Write($"Calculating hashes: {++index} of {total}");
                }
                Console.WriteLine("\nDone");

                var headers = files2.Select(x => Path.GetFileNameWithoutExtension(x));
                var header = ";" + string.Join(";", headers);
                Console.WriteLine(header);

                foreach (var left in files1)
                {
                    Console.Write(Path.GetFileNameWithoutExtension(left) + ";");
                    foreach (var right in files2)
                    {
                        var dist = PHash.DCT.HammingDistance(hashes[left], hashes[right]);
                        Console.Write($"{dist};");
                    }
                    Console.WriteLine();
                }
            }
            else 
            {
                Console.WriteLine("Unknown comparison type");
                return;
            }
        }

        private static void PopulateFiles(string ipath, IList<string> result)
        {
            var path = Path.GetFullPath(ipath);
            if (File.Exists(path))
            {
                result.Add(path);
            }
            else if (Directory.Exists(path))
            {
                var entries = Directory.EnumerateFileSystemEntries(path);
                foreach (var entry in entries)
                {
                    PopulateFiles(entry, result);
                }
            }
        }

        private static IEnumerable<TResult> RunInterTests<TSource, TResult>(IReadOnlyList<TSource> items, 
            Func<TSource, TSource, TResult> actor, bool selfTest = false)
        {
            var of = selfTest ? 0 : 1;
            for (int i = 0; i < items.Count - 1; i++)
            {
                for (int j = i + of; j < items.Count; j++)
                {
                    yield return actor(items[i], items[j]);
                }
            }
        }

        
    }
}
