using System.IO;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino.Fsck
{
	public class DiskItem
	{
		public DiskItem(string fullPath)
		{
			Assert.ArgumentNotNullOrEmpty(fullPath, "fullPath");

			Item = LoadItem(fullPath);
			FullPath = fullPath;
		}

		public SyncItem Item { get; private set; }
		public string FullPath { get; private set; }

		private SyncItem LoadItem(string path)
		{
			if (!File.Exists(path))
			{
				return null;
			}

			using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var reader = new StreamReader(fileStream))
				{
					var item = SyncItem.ReadItem(new Tokenizer(reader), true);

					Assert.IsNotNullOrEmpty(item.TemplateID, "{0}: TemplateID was not valid!", path);

					return item;
				}
			}
		}
	}
}
