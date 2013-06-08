using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Serialization.ObjectModel;

namespace Rhino.Filtering
{
	public interface IFilter
	{
		string Name { get; }
		FilterResult Includes(SyncItem item);
		FilterResult Includes(string itemPath, ID itemId, ID templateId, string templateName, Database database);

		Item[] GetRootItems();
	}
}
