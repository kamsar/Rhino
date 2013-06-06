﻿using Rhino.Filtering;
using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.DataProviders;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;

namespace Rhino
{
	public class FilteredSerializationDataProvider : SerializationDataProvider
	{
		readonly IFilter _filter;

		public FilteredSerializationDataProvider(string connectionStringName, string presetName) : base(connectionStringName)
		{
			_filter = new SerializationPresetFilter(presetName);
		}

		public FilteredSerializationDataProvider(string connectionStringName, IFilter filter) : base(connectionStringName)
		{
			Assert.ArgumentNotNull(filter, "filter");

			_filter = filter;
		}

		protected bool ShouldExecuteProvider(ID itemId)
		{
			var item = SerializedDatabase.GetItem(itemId);

			if (item == null) return false;

			var result = _filter.Includes(item);

			return result.IsIncluded;
		}

		public override bool HasChildren(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			
			if (!ShouldExecuteProvider(itemDefinition.ID)) return false;

			context.Abort();
			return GetChildIDs(itemDefinition, context).Count > 0; // we use GetChildIDs here so we can filter on included children
		}

		public override IDList GetChildIDs(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
			Assert.ArgumentNotNull(context, "context");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return null;

			context.Abort();
			var childIds = base.GetChildIDs(itemDefinition, context);

			var filteredChildren = new IDList();

			foreach (ID child in childIds)
			{
				if(ShouldExecuteProvider(child)) filteredChildren.Add(child);
			}

			// invoke the other data providers, if any, and *unique* the child IDs
			// this allows us to merge serialized items on top of an existing database item
			// (without this uniquing we'd get two identical children for items that were both
			// serialized AND in the other providers)
			var providers = Database.GetDataProviders();
			for (int i = context.Index + 1; i < context.ProviderCount; i++)
			{
				var otherChildIds = providers[i].GetChildIDs(itemDefinition, context);

				if (otherChildIds == null) continue;

				foreach (ID child in otherChildIds)
				{
					if(!filteredChildren.Contains(child)) filteredChildren.Add(child);
				}
			}

			return filteredChildren;
		}

		public override ID ResolvePath(string itemPath, CallContext context)
		{
			Assert.ArgumentNotNull(itemPath, "itemPath");

			var existingPath = SerializedDatabase.GetItem(itemPath);

			if (existingPath == null) return null;

			if (!ShouldExecuteProvider(existingPath.GetSitecoreId())) return null;

			context.Abort();
			return base.ResolvePath(itemPath, context);
		}

		public override ID GetParentID(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return null;

			context.Abort();
			return base.GetParentID(itemDefinition, context);
		}

		public override FieldList GetItemFields(ItemDefinition itemDefinition, VersionUri versionUri, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return null;

			context.Abort();
			return base.GetItemFields(itemDefinition, versionUri, context);
		}

		public override VersionUriList GetItemVersions(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return null;

			context.Abort();
			return base.GetItemVersions(itemDefinition, context);
		}

		public override ItemDefinition GetItemDefinition(ID itemId, CallContext context)
		{
			Assert.ArgumentNotNull(itemId, "itemId");

			if (!ShouldExecuteProvider(itemId)) return null;

			context.Abort();
			return base.GetItemDefinition(itemId, context);
		}

		public override bool CreateItem(ID itemId, string itemName, ID templateId, ItemDefinition parent, CallContext context)
		{
			// TODO filtering of child of parent, cross-provider error(?)
			return base.CreateItem(itemId, itemName, templateId, parent, context);
		}

		public override int AddVersion(ItemDefinition itemDefinition, VersionUri baseVersion, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return -1;

			context.Abort();
			return base.AddVersion(itemDefinition, baseVersion, context);
		}

		public override LanguageCollection GetLanguages(CallContext context)
		{
			return null;
		}

		public override bool CopyItem(ItemDefinition source, ItemDefinition destination, string copyName, ID copyId, CallContext context)
		{
			// TODO filtering of child of destination, cross-provider error
			return base.CopyItem(source, destination, copyName, copyId, context);
		}

		public override bool MoveItem(ItemDefinition itemDefinition, ItemDefinition destination, CallContext context)
		{
			// TODO filtering of child of destination, cross-provider error
			return base.MoveItem(itemDefinition, destination, context);
		}

		public override bool DeleteItem(ItemDefinition itemDefinition, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return false;

			context.Abort();
			base.DeleteItem(itemDefinition, context);

			return true;
		}

		public override bool RemoveVersion(ItemDefinition itemDefinition, VersionUri version, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return false;

			context.Abort();
			base.RemoveVersion(itemDefinition, version, context);

			return true;
		}

		public override bool RemoveVersions(ItemDefinition itemDefinition, Language language, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return false;

			context.Abort();
			base.RemoveVersions(itemDefinition, language, context);

			return true;
		}

		public override bool SaveItem(ItemDefinition itemDefinition, ItemChanges changes, CallContext context)
		{
			Assert.ArgumentNotNull(itemDefinition, "itemDefinition");

			if (!ShouldExecuteProvider(itemDefinition.ID)) return false;

			context.Abort();
			base.SaveItem(itemDefinition, changes, context);

			return true;
		}
	}
}