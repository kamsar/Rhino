using Sitecore.Data.Items;
using Sitecore.Data.Serialization.ObjectModel;

namespace Rhino.Filtering
{
	public interface IFilter
	{
		string Name { get; }
		FilterResult Includes(SyncItem item);

		Item[] GetRootItems();
	}
}
