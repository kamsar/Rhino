namespace Rhino.Fsck.Tests
{
	public class NoVersionsTest : ITest
	{
		public string Name
		{
			get { return "Warning: No Versions Exist"; }
		}

		public TestResult Execute(DiskItem contextItem, DiskItem[] allItems)
		{
			if (contextItem.Item.Versions.Count > 0) return new TestResult(this, true);

			return new TestResult(this, false, "This item had no versions in any language. This can be valid, but is highly unusual.");
		}
	}
}
