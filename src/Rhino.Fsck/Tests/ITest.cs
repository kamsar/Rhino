namespace Rhino.Fsck.Tests
{
	public interface ITest
	{
		string Name { get; }
		TestResult Execute(DiskItem contextItem, DiskItem[] allItems);
	}
}
