using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sitecore.Data;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino.Data
{
    public class SerializedDatabase
    {
		private readonly SerializedIndex _index;
	    private readonly string _serializationPath;

        public SerializedDatabase(string serializationPath)
        {
	        if (!Path.IsPathRooted(serializationPath))
		        serializationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, serializationPath);

            if (!Directory.Exists(serializationPath))
            {
                throw new Exception(string.Format("Path not found {0}, current Path {1}", Path.GetFullPath(serializationPath), AppDomain.CurrentDomain.BaseDirectory));
            }

            _index = new SerializedIndex(LoadItems(serializationPath));
	        _serializationPath = serializationPath;
        }

		public int Count { get { return _index.Count; } }

		public bool HasChildren(ID itemId)
		{
			return GetChildren(itemId).Length > 0;
		}

		public SyncItem[] GetChildren(ID parentId)
		{
			return _index.GetChildren(parentId);
		}

		public SyncItem[] GetItemsWithTemplate(ID templateId)
		{
			return _index.GetItemsWithTemplate(templateId);
		}

	    public SyncItem GetItem(ID id)
	    {
		    return _index.GetItem(id);
	    }

		public SyncItem GetItem(string path)
		{
			return _index.GetItem(path);
		}

		public void SaveItem(SyncItem syncItem)
		{
			Assert.ArgumentNotNull(syncItem, "syncItem");

			var newPath = GetSyncItemPath(syncItem);
			
			var parentPath = Path.GetDirectoryName(newPath);
			if(parentPath != null)
				Directory.CreateDirectory(parentPath);

			using (var fileStream = File.Open(newPath, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				using (var writer = new StreamWriter(fileStream))
				{
					syncItem.Serialize(writer);
				}
			}

			_index.UpdateIndexes(syncItem);
		}

		public void DeleteItem(SyncItem syncItem)
		{
			Assert.ArgumentNotNull(syncItem, "syncItem");

			var path = GetSyncItemPath(syncItem);

			if(File.Exists(path)) File.Delete(path); // remove the file

			var directory = PathUtils.StripPath(path);

			if(Directory.Exists(directory)) Directory.Delete(directory, true); // remove the directory that held child items, if it exists

			_index.ClearIndexes(syncItem.GetSitecoreId()); // remove from indexes
		}

		public void CopyItem(SyncItem source, ID destination, string copyName, ID copyId)
		{
			Assert.ArgumentNotNull(source, "source");
			Assert.ArgumentNotNull(destination, "destination");
			Assert.ArgumentNotNullOrEmpty(copyName, "copyName");
			Assert.ArgumentNotNull(copyId, "copyId");

			var destinationItem = GetItem(destination);

			Assert.IsNotNull(destinationItem, "Could not copy {0}  to {1} because the destination did not exist!", source.ID, destination);

			var newItem = source.Clone();

			newItem.ID = copyId.ToString();
			newItem.ParentID = destination.ToString();
			newItem.ItemPath = string.Concat(destinationItem.ItemPath, "/", copyName);
			newItem.Name = copyName;

			SaveItem(newItem);
		}

		public void MoveItem(SyncItem syncItem, ID newParent)
		{
			var newParentItem = _index.GetItem(newParent);

			Assert.IsNotNull(newParentItem, "New parent item {0} did not exist!", newParent);

			var oldRootPath = syncItem.ItemPath;
			var newRootPath = string.Concat(newParentItem.ItemPath, "/", syncItem.Name);

			var descendantItems = _index.GetDescendants(syncItem.GetSitecoreId());
	
			var oldSerializationPath = GetSyncItemPath(syncItem);
			
			// update the path and parent IDs to the new location
			syncItem.ParentID = newParent.ToString();
			syncItem.ItemPath = string.Concat(newParentItem.ItemPath, "/", syncItem.Name);

			// write the moved sync item to its new destination
			SaveItem(syncItem);

			// move descendant items by reserializing them and fixing their ItemPath
			if (descendantItems.Length > 0)
			{
				foreach (var descendant in descendantItems)
				{
					string oldPath = GetSyncItemPath(descendant);
	
					// save to new location
					descendant.ItemPath = descendant.ItemPath.Replace(oldRootPath, newRootPath);
					SaveItem(descendant);

					// remove old file location
					if (File.Exists(oldPath)) File.Delete(oldPath);
				}
			}

			// remove the old serialized item from disk
			if (File.Exists(oldSerializationPath)) File.Delete(oldSerializationPath);

			// remove the old serialized children folder from disk
			var directoryPath = PathUtils.StripPath(oldSerializationPath);
			if(Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
		}

		private string GetSyncItemPath(SyncItem syncItem)
		{
			// note: there is no overload of GetFilePath() that takes a custom root - so we use GetDirectoryPath and add the extension like GetFilePath does
			return PathUtils.GetDirectoryPath(new ItemReference(syncItem.DatabaseName, syncItem.ItemPath).ToString(), _serializationPath) + PathUtils.Extension;
		}

		private IEnumerable<SyncItem> LoadItems(string path)
		{
			Assert.ArgumentNotNullOrEmpty(path, "path");

			if (!Directory.Exists(path))
			{
				return Enumerable.Empty<SyncItem>();
			}

			var files = Directory.GetFiles(path, string.Format("*{0}", PathUtils.Extension), SearchOption.AllDirectories);
			var syncItems = new List<SyncItem>(files.Length);

			var writeLock = new object();
			Parallel.ForEach(files, subPath =>
				{
					var item = LoadItem(subPath);
					if (item != null)
					{
						lock (writeLock)
						{
							syncItems.Add(item);
						}
					}
				});

			return syncItems;
		}

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