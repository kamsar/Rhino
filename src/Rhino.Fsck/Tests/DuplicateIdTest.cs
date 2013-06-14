using System;
using System.Linq;

namespace Rhino.Fsck.Tests
{
	public class DuplicateIdTest : ITest
	{
		public string Name
		{
			get { return "Duplicate ID Check"; }
		}

		public TestResult Execute(DiskItem contextItem, DiskItem[] allItems)
		{
			var duplicates = allItems.Where(x =>
				x.Item.DatabaseName.Equals(contextItem.Item.DatabaseName, StringComparison.Ordinal) &&
				x.Item.ID.Equals(contextItem.Item.ID, StringComparison.Ordinal) &&
				x.FullPath != contextItem.FullPath)
					.ToArray();

			if (duplicates.Length == 0) return new TestResult(this, true);

			return new TestResult(this, false, contextItem.Item.ID + " was present in other files: " + string.Join(",", duplicates.Select(x => x.FullPath)));
		}
	}
}
