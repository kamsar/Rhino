using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rhino.Fsck.Tests
{
	public class TestResultCollection : ReadOnlyCollection<TestResult>
	{
		public TestResultCollection(IList<TestResult> basis) : base(basis)
		{
			
		}

		public bool ContainsFailures
		{
			get { return this.Any(x => !x.Passed); }
		}

		public TestResultCollection FailedTests
		{
			get
			{
				return new TestResultCollection(this.Where(x=>!x.Passed).ToList());
			}
		} 
	}
}
