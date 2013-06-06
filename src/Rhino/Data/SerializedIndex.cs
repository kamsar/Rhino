using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino.Data
{
	internal class SerializedIndex
	{
		private volatile List<SyncItem> _innerItems = new List<SyncItem>();
		private readonly object _innerItemsLock = new object();

		private volatile Dictionary<ID, SyncItem> _idLookup = new Dictionary<ID, SyncItem>();
		private readonly object _idLookupLock = new object();

		private volatile Dictionary<ID, SyncItem[]> _childrenLookup = new Dictionary<ID, SyncItem[]>();
		private readonly object _childrenLookupLock = new object();

		private volatile Dictionary<ID, SyncItem[]> _descendantsLookup = new Dictionary<ID, SyncItem[]>();
		private readonly object _descendantsLookupLock = new object();

		private volatile Dictionary<ID, SyncItem[]> _templateLookup = new Dictionary<ID, SyncItem[]>();
		private readonly object _templateLookupLock = new object();

		private volatile Dictionary<string, SyncItem> _pathLookup = new Dictionary<string, SyncItem>(StringComparer.OrdinalIgnoreCase);
		private readonly object _pathLookupLock = new object();

		public SerializedIndex(IEnumerable<SyncItem> items)
		{
			_innerItems.AddRange(items);
		}

		public SyncItem GetItem(ID id)
		{
			Assert.ArgumentNotNull(id, "id");

			SyncItem resultItem;
			if (!_idLookup.TryGetValue(id, out resultItem))
			{
				lock (_idLookupLock)
				{
					if (!_idLookup.TryGetValue(id, out resultItem))
					{
						string stringId = id.ToString();
						SyncItem item = _innerItems.Find(x => x.ID == stringId);
						if (item != null)
						{
							_idLookup.Add(id, item);
						}

						return item;
					}
				}
			}

			return resultItem;
		}

		public SyncItem GetItem(string path)
		{
			Assert.ArgumentNotNull(path, "path");

			SyncItem resultItem;
			if (!_pathLookup.TryGetValue(path, out resultItem))
			{
				lock (_pathLookupLock)
				{
					if (!_pathLookup.TryGetValue(path, out resultItem))
					{
						SyncItem item = _innerItems.Find(x => x.ItemPath.Equals(path, StringComparison.OrdinalIgnoreCase));
						if (item != null)
						{
							_pathLookup.Add(item.ItemPath, item);
						}

						return item;
					}
				}
			}

			return resultItem;
		}

		public SyncItem[] GetChildren(string path)
		{
			Assert.ArgumentNotNullOrEmpty(path, "path");

			var item = GetItem(path);

			if (item == null) return new SyncItem[0];

			return GetChildren(item.GetSitecoreId());
		}

		public SyncItem[] GetChildren(ID id)
		{
			Assert.ArgumentNotNull(id, "id");

			SyncItem[] resultItems;
			if (!_childrenLookup.TryGetValue(id, out resultItems))
			{
				lock (_childrenLookupLock)
				{
					if (!_childrenLookup.TryGetValue(id, out resultItems))
					{
						string stringId = id.ToString();
						var items = _innerItems.FindAll(x => x.ParentID == stringId).ToArray();

						_childrenLookup.Add(id, items);

						return items;
					}
				}
			}

			return resultItems;
		}

		public SyncItem[] GetDescendants(string path)
		{
			Assert.ArgumentNotNullOrEmpty(path, "path");

			var item = GetItem(path);

			if (item == null) return new SyncItem[0];

			return GetDescendants(item.GetSitecoreId());
		}

		public SyncItem[] GetDescendants(ID id)
		{
			Assert.ArgumentNotNull(id, "id");

			SyncItem[] resultItems;
			if (!_descendantsLookup.TryGetValue(id, out resultItems))
			{
				lock (_descendantsLookupLock)
				{
					if (!_descendantsLookup.TryGetValue(id, out resultItems))
					{
						var items = RecursiveGetDescendants(id).ToArray();

						_descendantsLookup.Add(id, items);

						return items;
					}
				}
			}

			return resultItems;
		}

		public SyncItem[] GetItemsWithTemplate(ID templateId)
		{
			Assert.ArgumentNotNull(templateId, "id");

			SyncItem[] resultItems;
			if (!_templateLookup.TryGetValue(templateId, out resultItems))
			{
				lock (_templateLookupLock)
				{
					if (!_templateLookup.TryGetValue(templateId, out resultItems))
					{
						string stringId = templateId.ToString();
						var items = _innerItems.FindAll(x => x.TemplateID == stringId).ToArray();

						_templateLookup.Add(templateId, items);

						return items;
					}
				}
			}

			return resultItems;
		}

		private IEnumerable<SyncItem> RecursiveGetDescendants(ID root)
		{
			var children = GetChildren(root).ToList();
			
			var allChildren = new List<SyncItem>(children);

			foreach (var child in children)
			{
				allChildren.AddRange(RecursiveGetDescendants(child.GetSitecoreId()));
			}

			return allChildren;
		}

		public void ClearIndexes(ID itemId)
		{
			lock (_innerItemsLock)
			{
				var stringId = itemId.ToString();

				for (int i = 0; i < _innerItems.Count; i++)
				{
					if (_innerItems[i].ID.Equals(stringId, StringComparison.Ordinal))
					{
						_innerItems.RemoveAt(i);

						ResetCacheIndexes();
						return;
					}
				}
			}
		}

		public void UpdateIndexes(SyncItem item)
		{
			lock (_innerItemsLock)
			{
				for (int i = 0; i < _innerItems.Count; i++)
				{
					if (_innerItems[i].ID == item.ID)
					{
						_innerItems[i] = item;

						ResetCacheIndexes();
						return;
					}
				}

				_innerItems.Add(item);
			}

			// TODO: could possibly find the item in the other indices and change them instead of nuking the whole index
			ResetCacheIndexes();
		}

		private void ResetCacheIndexes()
		{
			lock (_childrenLookupLock)
			{
				_childrenLookup.Clear();
			}

			lock (_descendantsLookupLock)
			{
				_descendantsLookup.Clear();
			}

			lock (_idLookupLock)
			{
				_idLookup.Clear();
			}

			lock (_pathLookupLock)
			{
				_pathLookup.Clear();
			}

			lock (_templateLookupLock)
			{
				_templateLookup.Clear();
			}
		}

		public int Count { get { return _innerItems.Count; } }
	}
}
