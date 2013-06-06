using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino
{
	internal static class SyncItemExtensions
	{
		public static ID GetSitecoreId(this SyncItem item)
		{
			Assert.ArgumentNotNull(item, "item");

			ID result;
			if (!ID.TryParse(item.ID, out result)) throw new ArgumentOutOfRangeException("item", "SyncItem did not have a parseable ID!");

			return result;
		}

		public static ID GetSitecoreParentId(this SyncItem item)
		{
			Assert.ArgumentNotNull(item, "item");

			ID result;
			if (!ID.TryParse(item.ParentID, out result)) throw new ArgumentOutOfRangeException("item", "SyncItem did not have a parseable ParentID!");

			return result;
		}

		public static SyncVersion GetVersion(this SyncItem item, VersionUri uri)
		{
			string versionString = uri.Version.Number.ToString(CultureInfo.InvariantCulture);

			return item.Versions.FirstOrDefault(x => x.Language.Equals(uri.Language.Name, StringComparison.OrdinalIgnoreCase) && x.Version == versionString);
		}

		public static SyncItem Clone(this SyncItem item)
		{
			using (var ms = new MemoryStream())
			{
				using (var writer = new StreamWriter(ms))
				{
					item.Serialize(writer);

					writer.Flush();

					ms.Seek(0, SeekOrigin.Begin);

					using (var reader = new StreamReader(ms))
					{
						return SyncItem.ReadItem(new Tokenizer(reader));
					}
				}
			}
		}
	}
}
