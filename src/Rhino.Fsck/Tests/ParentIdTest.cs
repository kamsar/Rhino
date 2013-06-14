using System;
using System.IO;
using System.Linq;
using Sitecore.Data.Serialization;

namespace Rhino.Fsck.Tests
{
	/// <summary>
	/// This test verifies that the serialized item in the parent filesystem directory has an ID that matches the current item's parent ID
	/// </summary>
	/// <remarks>
	///	This test will pass if the parent directory has no serialized item in it
	/// </remarks>
	public class ParentIdTest : ITest
	{
		public string Name
		{
			get { return "Parent ID Verification"; }
		}

		public TestResult Execute(DiskItem contextItem, DiskItem[] allItems)
		{
			var parent = Path.GetDirectoryName(contextItem.FullPath) + PathUtils.Extension;

			if (File.Exists(parent))
			{
				var parentItem = allItems.First(x => x.FullPath.Equals(parent, StringComparison.Ordinal));

				bool result = parentItem.Item.ID.Equals(contextItem.Item.ParentID, StringComparison.Ordinal);

				if(result) return new TestResult(this, true);
				
				return new TestResult(this, false, string.Format("Parent ID: {0} did not match actual serialized parent ID {1}", contextItem.Item.ParentID, parentItem.Item.ID));
			}

			return new TestResult(this, true);
		}
	}
}
