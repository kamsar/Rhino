using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.Fsck.Tests
{
	public static class TestRunner
	{
		public static ItemTestResult[] ExecuteTests(DiskItem[] items, ITest[] tests)
		{
			var results = new List<ItemTestResult>(items.Length);

			ExecuteTests(items, tests, results.Add);

			return results.ToArray();
		}

		public static void ExecuteTests(DiskItem[] items, ITest[] tests, Action<ItemTestResult> resultCallback)
		{
			foreach (var item in items)
			{
				var result = new ItemTestResult(item, new TestResultCollection(tests.Select(x => x.Execute(item, items)).ToList()));

				resultCallback(result);
			}
		}
	}
}
