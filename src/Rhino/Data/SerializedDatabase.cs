using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sitecore.Data;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;
using FileSystemEventArgs = System.IO.FileSystemEventArgs;

namespace Rhino.Data
{
	public class SerializedDatabase
	{
		private readonly SerializedIndex _index;
		private readonly string _serializationPath;
		private readonly FileSystemWatcher _watcher;

		public SerializedDatabase(string serializationPath, bool watchForChanges)
		{
			if (!Path.IsPathRooted(serializationPath))
				serializationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, serializationPath);

			if (!Directory.Exists(serializationPath))
			{
				throw new Exception(string.Format("Path not found {0}, current Path {1}", Path.GetFullPath(serializationPath), AppDomain.CurrentDomain.BaseDirectory));
			}

			_index = new SerializedIndex(LoadItems(serializationPath));
			_serializationPath = serializationPath;

			if (watchForChanges)
			{
				_watcher = new FileSystemWatcher(serializationPath, "*" + PathUtils.Extension) { IncludeSubdirectories = true };
				_watcher.Changed += OnFileChanged;
				_watcher.Created += OnFileChanged;
				_watcher.Deleted += OnFileChanged;
				_watcher.Renamed += OnFileRenamed;
				_watcher.EnableRaisingEvents = true;
			}
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
			ID idPath;
			if (ID.TryParse(path, out idPath)) return GetItem(idPath);

			path = path.TrimEnd('/');

			if (path == string.Empty) path = "/sitecore"; // the content editor expects "/" to resolve to /sitecore (verified against SQL provider)

			return _index.GetItem(path);
		}

		public void SaveItem(SyncItem syncItem)
		{
			Assert.ArgumentNotNull(syncItem, "syncItem");

			var newPath = GetPhysicalSyncItemPath(syncItem);

			var parentPath = Path.GetDirectoryName(newPath);
			if (parentPath != null)
				Directory.CreateDirectory(parentPath);

			using (new WatcherDisabler(_watcher))
			{
				using (var fileStream = File.Open(newPath, FileMode.Create, FileAccess.Write, FileShare.Write))
				{
					using (var writer = new StreamWriter(fileStream))
					{
						syncItem.Serialize(writer);
					}
				}
			}

			_index.UpdateIndexes(syncItem);
		}

		public void DeleteItem(SyncItem syncItem)
		{
			Assert.ArgumentNotNull(syncItem, "syncItem");

			var path = GetPhysicalSyncItemPath(syncItem);
			var directory = PathUtils.StripPath(path);

			using (new WatcherDisabler(_watcher))
			{
				if (File.Exists(path)) File.Delete(path); // remove the file
				if (Directory.Exists(directory)) Directory.Delete(directory, true); // remove the directory that held child items, if it exists
			}

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
			Assert.ArgumentNotNull(syncItem, "syncItem");
			Assert.ArgumentNotNullOrEmpty(newParent, "newParent");

			var newParentItem = _index.GetItem(newParent);

			Assert.IsNotNull(newParentItem, "New parent item {0} did not exist!", newParent);

			var oldRootPath = syncItem.ItemPath;
			var newRootPath = string.Concat(newParentItem.ItemPath, "/", syncItem.Name);

			var descendantItems = _index.GetDescendants(syncItem.GetSitecoreId());

			// update the path and parent IDs to the new location
			syncItem.ParentID = newParent.ToString();
			syncItem.ItemPath = string.Concat(newParentItem.ItemPath, "/", syncItem.Name);

			// write the moved sync item to its new destination
			SaveItem(syncItem);

			MoveDescendants(oldRootPath, newRootPath, syncItem.DatabaseName, descendantItems);
		}

		public void SaveAndRenameItem(SyncItem renamedItem, string oldName)
		{
			var oldRootPath = renamedItem.ItemPath.Substring(0, renamedItem.ItemPath.LastIndexOf('/') + 1) + oldName;
			var newRootPath = renamedItem.ItemPath;

			var descendantItems = _index.GetDescendants(renamedItem.GetSitecoreId());

			// write the moved sync item to its new destination
			SaveItem(renamedItem);

			MoveDescendants(oldRootPath, newRootPath, renamedItem.DatabaseName, descendantItems);
		}

		private void MoveDescendants(string oldSitecorePath, string newSitecorePath, string databaseName, SyncItem[] descendantItems)
		{
			// if the paths were the same, no moving occurs (this can happen when saving templates, which spuriously can report "renamed" when they are not actually any such thing)
			if (oldSitecorePath.Equals(newSitecorePath, StringComparison.OrdinalIgnoreCase)) return;

			var oldSerializationPath = GetPhysicalPath(new ItemReference(databaseName, oldSitecorePath));

			// move descendant items by reserializing them and fixing their ItemPath
			if (descendantItems.Length > 0)
			{
				foreach (var descendant in descendantItems)
				{
					string oldPath = GetPhysicalSyncItemPath(descendant);

					// save to new location
					descendant.ItemPath = descendant.ItemPath.Replace(oldSitecorePath, newSitecorePath);
					SaveItem(descendant);

					// remove old file location
					if (File.Exists(oldPath))
					{
						using (new WatcherDisabler(_watcher))
						{
							File.Delete(oldPath);
						}
					}
				}
			}

			using (new WatcherDisabler(_watcher))
			{
				// remove the old serialized item from disk
				if (File.Exists(oldSerializationPath)) File.Delete(oldSerializationPath);

				// remove the old serialized children folder from disk
				var directoryPath = PathUtils.StripPath(oldSerializationPath);
				if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
			}
		}

		private string GetPhysicalSyncItemPath(SyncItem syncItem)
		{
			return GetPhysicalPath(new ItemReference(syncItem.DatabaseName, syncItem.ItemPath));
		}

		private string GetPhysicalPath(ItemReference reference)
		{
			Assert.ArgumentNotNull(reference, "reference");

			// note: there is no overload of GetFilePath() that takes a custom root - so we use GetDirectoryPath and add the extension like GetFilePath does
			return PathUtils.GetDirectoryPath(reference.ToString(), _serializationPath) + PathUtils.Extension;
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

		private void OnFileChanged(object source, FileSystemEventArgs args)
		{
			OnFileChanged(args.FullPath, args.ChangeType);
		}

		private void OnFileRenamed(object source, RenamedEventArgs args)
		{
			OnFileChanged(args.FullPath, args.ChangeType);
		}

		private void OnFileChanged(string path, WatcherChangeTypes changeType)
		{
			if (changeType == WatcherChangeTypes.Created || changeType == WatcherChangeTypes.Changed || changeType == WatcherChangeTypes.Renamed)
			{
				Log.Info(string.Format("Serialized item {0} changed ({1}), reloading caches.", path, changeType), this);

				const int retries = 5;
				for (int i = 0; i < retries; i++)
				{
					try
					{
						var syncItem = LoadItem(path);
						if (syncItem != null)
						{
							_index.UpdateIndexes(syncItem);

							// TODO: nuclear bomb when all we need is a nerf gun
							Sitecore.Caching.CacheManager.ClearAllCaches();
						}
					}
					catch (IOException iex)
					{
						// this is here because FSW can tell us the file has changed
						// BEFORE it's done with writing. So if we get access denied,
						// we wait 500ms and retry up to 5x before rethrowing
						if (i < retries - 1)
						{
							Thread.Sleep(500);
							continue;
						}

						Log.Error("Failed to read serialization file", iex, this);
					}

					break;
				}
			}

			if (changeType == WatcherChangeTypes.Deleted)
			{
				Log.Info(string.Format("Serialized item {0} deleted, reloading caches.", path), this);

				var root = _serializationPath;
				// if the path does not end with a backslash (\) MakeItemPath won't generate a proper ItemReference (//master vs /master)
				if (!root.EndsWith(@"\")) root += @"\";

				var itemPath = ItemReference.Parse(PathUtils.MakeItemPath(path, root));

				var item = _index.GetItem(itemPath.Path);

				if (item != null)
				{
					_index.ClearIndexes(item.GetSitecoreId());

					// TODO: nuclear bomb when all we need is a nerf gun
					Sitecore.Caching.CacheManager.ClearAllCaches();
				}
			}
		}
	}
}