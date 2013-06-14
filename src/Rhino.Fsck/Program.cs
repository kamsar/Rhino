using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Rhino.Fsck.Tests;
using Sitecore.Data.Serialization;
using Sitecore.Diagnostics;

namespace Rhino.Fsck
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Specify the path to the serialized items to check as the first argument.");
				Console.WriteLine("Optionally, if checking a single database folder, pass the database name as the second argument.");
				Console.WriteLine(@"Note: checking subfolders of a database folder (eg master\sitecore\content) is not supported.");
				Console.WriteLine();
				Console.WriteLine(@"Rhino.Fsck c:\sitecore\data\serialization");
				Console.WriteLine(@"Rhino.Fsck c:\sitecore\data\serialization\master master");
				return;
			}

			if (!Directory.Exists(args[0]))
			{
				Console.WriteLine("Directory {0} does not exist.", args[0]);
				return;
			}

			Console.Write("Loading serialized items...");

			try
			{
				var items = GetItems(args[0]);
				Console.WriteLine(items.Length);

				var tests = new ITest[]
					{
						(args.Length < 2) ? new PathTest(args[0]) : new PathTest(args[0], args[1]),
						new ParentIdTest(),
						new DuplicateIdTest()
					};

				Console.WriteLine("Testing...");

				var results = TestRunner.ExecuteTests(items, tests);

				WriteFailedResults(results);
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
				Console.ResetColor();
				return;
			}

			Console.WriteLine("Complete.");
		}

		private static void WriteFailedResults(IEnumerable<ItemTestResult> results)
		{
			int failures = 0;

			foreach (var result in results)
			{
				if (result.ContainsFailures)
				{
					failures++;
					Console.WriteLine("{0}:", result.Item.FullPath);

					foreach (var failure in result.Results.FailedTests)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("\tFAILED: {0}", failure.TestName);

						if (!string.IsNullOrWhiteSpace(failure.Message))
						{
							Console.ForegroundColor = ConsoleColor.White;
							Console.WriteLine("\t" + failure.Message);
						}
					}

					Console.ResetColor();
				}
			}

			Console.WriteLine();

			if (failures == 0)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("No inconsistencies detected!");
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("{0} item{1} data inconsistencies!", failures, failures == 1 ? " has" : "s have");
			}

			Console.ResetColor();
			Console.WriteLine();
		}

		private static DiskItem[] GetItems(string rootPath)
		{
			Assert.ArgumentNotNullOrEmpty(rootPath, "path");

			if (!Directory.Exists(rootPath))
			{
				return new DiskItem[0];
			}

			var files = Directory.GetFiles(rootPath, string.Format("*{0}", PathUtils.Extension), SearchOption.AllDirectories);
			var diskItems = new List<DiskItem>(files.Length);

			var writeLock = new object();
			Parallel.ForEach(files, subPath =>
			{
				var item = new DiskItem(subPath);
				lock (writeLock)
				{
					diskItems.Add(item);
				}
			});

			return diskItems.ToArray();
		}
	}
}
