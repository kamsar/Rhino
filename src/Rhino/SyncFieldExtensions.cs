using System;
using Sitecore.Data;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino
{
	internal static class SyncFieldExtensions
	{
		public static ID GetSitecoreId(this SyncField item)
		{
			Assert.ArgumentNotNull(item, "item");

			ID result;
			if (!ID.TryParse(item.FieldID, out result)) throw new ArgumentOutOfRangeException("item", "SyncField did not have a parseable FieldID!");

			return result;
		}
	}
}
