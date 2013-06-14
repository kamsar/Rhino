namespace Rhino.Fsck.Tests
{
	public class ItemTestResult
	{
		public ItemTestResult(DiskItem item, TestResultCollection results)
		{
			Item = item;
			Results = results;
		}

		public DiskItem Item { get; private set; }
		public TestResultCollection Results { get; private set; }

		public bool ContainsFailures
		{
			get { return Results.ContainsFailures; }
		}
	}
}
