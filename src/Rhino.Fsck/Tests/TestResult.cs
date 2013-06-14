namespace Rhino.Fsck.Tests
{
	public class TestResult
	{
		public TestResult(ITest test, bool passed) : this(test, passed, null)
		{
			
		}

		public TestResult(ITest test, bool passed, string message)
		{
			TestName = test.Name;
			Passed = passed;
			Message = message;
		}

		public string TestName { get; private set; }
		public bool Passed { get; private set; }
		public string Message { get; private set; }
	}
}
