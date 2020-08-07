using ImageSim.ViewModels;
using ImageSim.ViewModels.FileTree;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tests
{
    class FileTreeVMTests
    {
        readonly struct TestEntry
        {
            public readonly TreeEntryVM Entry;
            public readonly string Name;
            public readonly string Path;
            public readonly int ChildCount;

            public TestEntry(TreeEntryVM entry, string name, string path, int childCount)
            {
                Entry = entry;
                Name = name;
                Path = path;
                ChildCount = childCount;
            }
        }

        private FileTreeVM CreateTestVM()
        {
            var list = new FileListVM(new ImageSim.Services.DummyFileService());
            list.AddFile("A:\\B.txt");
            list.AddFile("A:\\C\\D\\E.txt");
            list.AddFile("F:\\G\\H.txt");
            list.AddFile("F:\\G\\I.txt");
            list.AddFile("X:\\Y\\Z.txt");

            return new FileTreeVM(list);
        }

        private void AssertTreeStructure(IList<TestEntry> tst)
        {
            foreach (var item in tst)
            {
                Assert.AreEqual(item.Name, item.Entry.Name);
                Assert.AreEqual(item.Path, item.Entry.FullPath);
                if (item.ChildCount >= 0)
                {
                    Assert.AreEqual(item.ChildCount, item.Entry.Children.Count);
                }
                else
                {
                    Assert.IsNull(item.Entry.Children);
                }
            }
        }

        [Test]
        public void Test_FileTreeVMCreation()
        {
            var vm = CreateTestVM();
            Assert.AreEqual(3, vm.Entries.Count);
            var a = vm.Entries.First(x => x.Name == "A:");
            var b = a.Children.First(x => x.Name == "B.txt");
            var c = a.Children.First(x => x.Name == "C");
            var d = c.Children.First(x => x.Name == "D");
            var e = d.Children.First(x => x.Name == "E.txt");
            var f = vm.Entries.First(x => x.Name == "F:");
            var g = f.Children.First(x => x.Name == "G");
            var h = g.Children.First(x => x.Name == "H.txt");
            var i = g.Children.First(x => x.Name == "I.txt");
            var x = vm.Entries.First(x => x.Name == "X:");
            var y = x.Children.First(x => x.Name == "Y");
            var z = y.Children.First(x => x.Name == "Z.txt");

            var tst = new List<TestEntry>()
            {
                new TestEntry(a, "A:", "A:", 2),
                new TestEntry(b, "B.txt", "A:\\B.txt", -1),
                new TestEntry(c, "C", "A:\\C", 1),
                new TestEntry(d, "D", "A:\\C\\D", 1),
                new TestEntry(e, "E.txt", "A:\\C\\D\\E.txt", -1),
                new TestEntry(f, "F:", "F:", 1),
                new TestEntry(g, "G", "F:\\G", 2),
                new TestEntry(h, "H.txt", "F:\\G\\H.txt", -1),
                new TestEntry(i, "I.txt", "F:\\G\\I.txt", -1),
                new TestEntry(x, "X:", "X:", 1),
                new TestEntry(y, "Y", "X:\\Y", 1),
                new TestEntry(z, "Z.txt", "X:\\Y\\Z.txt", -1),
            };

            AssertTreeStructure(tst);
        }

        [Test]
        public void Test_FileTreeVM_Remove()
        {
            var vm = CreateTestVM();

            Assert.IsFalse(vm.RemoveEntry("Nonexist"));
            Assert.IsFalse(vm.RemoveEntry("A:\\Nonexist"));
            Assert.IsFalse(vm.RemoveEntry("A:\\Nonexist.txt"));

            Assert.IsTrue(vm.RemoveEntry("A:\\B.txt"));

            var a = vm.Entries.First(x => x.Name == "A:");
            var c = a.Children.First(x => x.Name == "C");
            var d = c.Children.First(x => x.Name == "D");
            var e = d.Children.First(x => x.Name == "E.txt");
            var f = vm.Entries.First(x => x.Name == "F:");
            var g = f.Children.First(x => x.Name == "G");
            var h = g.Children.First(x => x.Name == "H.txt");
            var i = g.Children.First(x => x.Name == "I.txt");
            var x = vm.Entries.First(x => x.Name == "X:");
            var y = x.Children.First(x => x.Name == "Y");
            var z = y.Children.First(x => x.Name == "Z.txt");

            var tst = new List<TestEntry>()
            {
                new TestEntry(a, "A:", "A:", 1),
                new TestEntry(c, "C", "A:\\C", 1),
                new TestEntry(d, "D", "A:\\C\\D", 1),
                new TestEntry(e, "E.txt", "A:\\C\\D\\E.txt", -1),
                new TestEntry(f, "F:", "F:", 1),
                new TestEntry(g, "G", "F:\\G", 2),
                new TestEntry(h, "H.txt", "F:\\G\\H.txt", -1),
                new TestEntry(i, "I.txt", "F:\\G\\I.txt", -1),
                new TestEntry(x, "X:", "X:", 1),
                new TestEntry(y, "Y", "X:\\Y", 1),
                new TestEntry(z, "Z.txt", "X:\\Y\\Z.txt", -1),
            };

            AssertTreeStructure(tst);

            Assert.IsTrue(vm.RemoveEntry("A:\\C\\D\\E.txt"));
            var tst2 = new List<TestEntry>()
            {
                new TestEntry(f, "F:", "F:", 1),
                new TestEntry(g, "G", "F:\\G", 2),
                new TestEntry(h, "H.txt", "F:\\G\\H.txt", -1),
                new TestEntry(i, "I.txt", "F:\\G\\I.txt", -1),
                new TestEntry(x, "X:", "X:", 1),
                new TestEntry(y, "Y", "X:\\Y", 1),
                new TestEntry(z, "Z.txt", "X:\\Y\\Z.txt", -1),
            };
            AssertTreeStructure(tst2);

            Assert.IsTrue(vm.RemoveEntry("F:"));
            var tst3 = new List<TestEntry>()
            {
                new TestEntry(x, "X:", "X:", 1),
                new TestEntry(y, "Y", "X:\\Y", 1),
                new TestEntry(z, "Z.txt", "X:\\Y\\Z.txt", -1),
            };
            AssertTreeStructure(tst3);
        }
    }
}
