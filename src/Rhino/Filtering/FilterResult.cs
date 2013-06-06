namespace Rhino.Filtering
{
	public class FilterResult
	{
		public FilterResult(bool included)
		{
			IsIncluded = included;
		}

		public FilterResult(string justification)
		{
			IsIncluded = false;
			Justification = justification;
		}

		public bool IsIncluded { get; private set; }
		public string Justification { get; private set; }
	}
}
