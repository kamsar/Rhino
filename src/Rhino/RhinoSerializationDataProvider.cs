using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Rhino.Data;
using Sitecore;
using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.DataProviders;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Serialization;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Version = Sitecore.Data.Version;

namespace Rhino
{
	public class RhinoSerializationDataProvider : DataProvider
	{
		private readonly SerializedDatabase _database;

		public RhinoSerializationDataProvider(string connectionStringName)
		{
			Assert.ArgumentNotNullOrEmpty(connectionStringName, "connectionStringName");

			var connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;

			Log.Info("RhinoSerializationDataProvider is initializing...", this);
			var sw = new Stopwatch();
			sw.Start();

			_database = new SerializedDatabase(connectionString);

			sw.Stop();
			Log.Info(string.Format("Rhino: loaded {0} serialized items into memory index in {1}ms", _database.Count, sw.ElapsedMilliseconds), this);
		}

		public override bool HasChildren(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			return _database.HasChildren(itemDefinition.ID);
		}

		public override IDList GetChildIDs(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (itemDefinition == ItemDefinition.Empty) return new IDList();

			var children = _database.GetChildren(itemDefinition.ID);
			var ids = new IDList();

			if (children == null)
			{
				return ids;
			}

			foreach (var syncItem in children)
			{
				ids.Add(syncItem.GetSitecoreId());
			}

			return ids;
		}

		public override ID ResolvePath(string itemPath, CallContext context)
		{
			Assert.ArgumentNotNullOrEmpty(itemPath, "itemPath");

			ID idPath;
			if (ID.TryParse(itemPath, out idPath)) return idPath;

			itemPath = itemPath.TrimEnd('/');

			if (itemPath == string.Empty) itemPath = "/sitecore"; // the content editor expects "/" to resolve to /sitecore (verified against SQL provider)

			var syncItem = _database.GetItem(itemPath);

			if (syncItem == null)
				return ID.Null;

			return syncItem.GetSitecoreId();
		}

		public override IdCollection GetTemplateItemIds(CallContext context)
		{
			var templatesRoot = _database.GetItemsWithTemplate(TemplateIDs.Template);
			var ids = new IdCollection();

			foreach (var syncItem in templatesRoot)
			{
				ids.Add(syncItem.GetSitecoreId());
			}

			return ids;
		}

		public override ID GetParentID(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (itemDefinition == ItemDefinition.Empty) return null;

			var syncItem = _database.GetItem(itemDefinition.ID);

			if (syncItem == null) return null;

			var parentId = syncItem.GetSitecoreParentId();

			if (parentId.IsNull)
				return null;

			return parentId;
		}

		public override FieldList GetItemFields(ItemDefinition itemDefinition, VersionUri versionUri, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(versionUri, "versionUri");

			if (itemDefinition == ItemDefinition.Empty) return new FieldList();

			var syncItem = _database.GetItem(itemDefinition.ID);

			if (syncItem == null) return new FieldList();

			var fields = new FieldList();

			foreach (var sharedField in syncItem.SharedFields)
			{
				fields.Add(sharedField.GetSitecoreId(), sharedField.FieldValue);
			}

			var syncVersion = syncItem.GetVersion(versionUri);

			if (syncVersion == null) return fields;

			foreach (var versionedField in syncVersion.Fields)
			{
				fields.Add(versionedField.GetSitecoreId(), versionedField.FieldValue);
			}

			return fields;
		}

		public override VersionUriList GetItemVersions(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (itemDefinition == ItemDefinition.Empty) return new VersionUriList();

			var versions = new VersionUriList();

			var syncItem = _database.GetItem(itemDefinition.ID);

			foreach (var syncVersion in syncItem.Versions)
			{
				versions.Add(Language.Parse(syncVersion.Language), Version.Parse(syncVersion.Version));
			}

			return versions;
		}

		public override ItemDefinition GetItemDefinition(ID itemId, CallContext context)
		{
			Assert.ArgumentNotNull(itemId, "itemId");

			var syncItem = _database.GetItem(itemId);

			if (syncItem == null) return null;

			Assert.IsNotNullOrEmpty(syncItem.Name, "Invalid serialized item name for {0}", itemId);
			Assert.IsNotNullOrEmpty(syncItem.TemplateID, "Invalid serialized template ID for {0} ({1})", itemId, syncItem.Name);
			Assert.IsNotNullOrEmpty(syncItem.BranchId, "Invalid serialized branch ID for {0} ({1})", itemId, syncItem.Name);

			return new ItemDefinition(itemId, syncItem.Name, ID.Parse(syncItem.TemplateID), ID.Parse(syncItem.BranchId));
		}

		public override bool CreateItem(ID itemId, string itemName, ID templateId, ItemDefinition parent, CallContext context)
		{
			Assert.ArgumentNotNull(itemId, "itemId");
			Assert.ArgumentNotNullOrEmpty(itemName, "itemName");
			Assert.ArgumentNotNull(templateId, "templateId");
			Assert.ArgumentNotNull(parent, "parent");
			Assert.ArgumentNotNull(context, "context");

			var parentItem = _database.GetItem(parent.ID);

			Assert.IsNotNull(parentItem, "Parent item {0} did not exist in the serialization store!", parent.ID);

			var template = TemplateManager.GetTemplate(templateId, context.DataManager.Database);

			Assert.IsNotNull(template, "The template ID {0} could not be found in {1}!", templateId, context.DataManager.Database.Name);

			string newItemFullPath = string.Concat(parentItem.ItemPath, "/", itemName);

			var syncItem = new SyncItem
				{
					ID = itemId.ToString(),
					Name = itemName,
					TemplateID = templateId.ToString(),
					TemplateName = template.Name,
					ParentID = parent.ID.ToString(),
					DatabaseName = context.DataManager.Database.Name,
					ItemPath = newItemFullPath,
					MasterID = ID.Null.ToString()
				};

			_database.SaveItem(syncItem);

			return true;
		}

		public override int AddVersion(ItemDefinition itemDefinition, VersionUri baseVersion, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(baseVersion, "baseVersion");

			var existingItem = _database.GetItem(itemDefinition.ID);

			int newVersionNumber;

			Assert.IsNotNull(existingItem, "Existing item {0} did not exist in the serialization store!", itemDefinition.ID);

			if (baseVersion.Version.Number > 0)
			{
				var baseSyncVersion = existingItem.GetVersion(baseVersion);

				Assert.IsNotNull(baseSyncVersion, "Base version {0}#{1} did not exist on {2}!", baseVersion.Language.Name,
								baseVersion.Version.Number, itemDefinition.ID);

				var newSyncVersion = baseSyncVersion.Clone();

				// the new version will be the max of the base version + 1, OR the highest existing version number in the current language + 1
				newVersionNumber = Math.Max(baseVersion.Version.Number + 1, existingItem.Versions
																							.Where(x => x.Language == baseVersion.Language.Name)
																							.Max(x => int.Parse(x.Version)) + 1);

				newSyncVersion.Version = newVersionNumber.ToString(CultureInfo.InvariantCulture);

				existingItem.Versions.Add(newSyncVersion);
			}
			else
			{
				newVersionNumber = 1;
				existingItem.AddVersion(baseVersion.Language.Name, newVersionNumber.ToString(CultureInfo.InvariantCulture), ID.NewID.ToString());
			}

			_database.SaveItem(existingItem);

			return newVersionNumber;
		}

		public override LanguageCollection GetLanguages(CallContext context)
		{
			// TODO: remove when doing partial database serialization
			var languages = _database.GetItemsWithTemplate(TemplateIDs.Language);

			return new LanguageCollection(languages.Select(x => Language.Parse(x.Name)));
		}

		public override bool CopyItem(ItemDefinition source, ItemDefinition destination, string copyName, ID copyId, CallContext context)
		{
			Assert.ArgumentNotNull(source, "source");
			Assert.ArgumentNotNull(destination, "destination");
			Assert.ArgumentNotNullOrEmpty(copyName, "copyName");
			Assert.ArgumentNotNull(copyId, "copyId");

			var existingItem = _database.GetItem(source.ID);

			Assert.IsNotNull(existingItem, "Could not copy {0} because it did not exist!", source.ID);

			_database.CopyItem(existingItem, destination.ID, copyName, copyId);

			return true;
		}

		public override bool MoveItem(ItemDefinition itemDefinition, ItemDefinition destination, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(destination, "destination");

			var existingItem = _database.GetItem(itemDefinition.ID);

			Assert.IsNotNull(existingItem, "Item {0} to move did not exist!", itemDefinition.ID);

			_database.MoveItem(existingItem, destination.ID);

			return true;
		}

		public override bool DeleteItem(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			var existingItem = _database.GetItem(itemDefinition.ID);

			if (existingItem == null) return true; // it was already gone (probably will never occur)

			_database.DeleteItem(existingItem);

			return true;
		}

		public override bool RemoveVersion(ItemDefinition itemDefinition, VersionUri version, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(version, "version");

			var existingItem = _database.GetItem(itemDefinition.ID);

			Assert.IsNotNull(existingItem, "Existing item {0} did not exist in the serialization store!", itemDefinition.ID);

			var syncVersion = existingItem.GetVersion(version);

			Assert.IsNotNull(syncVersion, "Version to remove {0}#{1} did not exist on {2}!", version.Language.Name, version.Version.Number, itemDefinition.ID);

			existingItem.Versions.Remove(syncVersion);

			_database.SaveItem(existingItem);

			return true;
		}

		public override bool RemoveVersions(ItemDefinition itemDefinition, Language language, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(language, "language");

			var existingItem = _database.GetItem(itemDefinition.ID);

			Assert.IsNotNull(existingItem, "Existing item {0} did not exist in the serialization store!", itemDefinition.ID);

			for (int i = existingItem.Versions.Count; i > 0; i--)
			{
				if(existingItem.Versions[i].Language.Equals(language.Name, StringComparison.OrdinalIgnoreCase))
					existingItem.Versions.RemoveAt(i);
			}

			_database.SaveItem(existingItem);

			return true;
		}

		public override bool SaveItem(ItemDefinition itemDefinition, ItemChanges changes, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(changes, "changes");

			var existingItem = _database.GetItem(itemDefinition.ID);

			Assert.IsNotNull(existingItem, "Existing item {0} did not exist in the serialization store!", itemDefinition.ID);

			var savedItem = ItemSynchronization.BuildSyncItem(changes.Item);

			_database.SaveItem(savedItem);

			return true;
		}
	}
}