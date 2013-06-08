using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Data.Serialization.Presets;
using Sitecore.Diagnostics;
using Sitecore.StringExtensions;

namespace Rhino.Filtering
{
	public class SerializationPresetFilter : IFilter
	{
		private readonly IList<IncludeEntry> _preset;

		public SerializationPresetFilter(string presetName)
		{
			Assert.IsNotNullOrEmpty(presetName, "presetName");

			var config = Factory.GetConfigNode("serialization/" + presetName);
			
			if (config == null)
				throw new InvalidOperationException("Preset " + presetName + " is undefined in configuration.");

			_preset = PresetFactory.Create(config);
		}

		public string Name { get { return "Serialization Preset"; } }

		public FilterResult Includes(SyncItem item)
		{
			var result = new FilterResult(true);
			FilterResult priorityResult = null;
			foreach (var entry in _preset)
			{
				result = Includes(entry, item);

				if (result.IsIncluded) return result; // it's definitely included if anything includes it
				if (!string.IsNullOrEmpty(result.Justification)) priorityResult = result; // a justification means this is probably a more 'important' fail than others
			}

			return priorityResult ?? result; // return the last failure
		}

		public FilterResult Includes(string itemPath, ID itemId, ID templateId, string templateName, Database database)
		{
			var result = new FilterResult(true);
			FilterResult priorityResult = null;
			foreach (var entry in _preset)
			{
				result = Includes(entry, itemPath, itemId, templateId, templateName, database);

				if (result.IsIncluded) return result; // it's definitely included if anything includes it
				if (!string.IsNullOrEmpty(result.Justification)) priorityResult = result; // a justification means this is probably a more 'important' fail than others
			}

			return priorityResult ?? result; // return the last failure
		}

		public Item[] GetRootItems()
		{
			var items = new List<Item>();

			foreach (var include in _preset)
			{
				var item = Factory.GetDatabase(include.Database).GetItem(include.Path);

				if (item != null) items.Add(item);
				else Log.Warn("Unable to resolve root item for serialization preset {0}:{1}".FormatWith(include.Database, include.Path), this);
			}

			return items.ToArray();
		}

		/// <summary>
		/// Checks if a preset includes a given serialized item
		/// </summary>
		protected FilterResult Includes(IncludeEntry entry, SyncItem item)
		{
			// check for db match
			if (item.DatabaseName != entry.Database) return new FilterResult(false);

			// check for path match
			if (!item.ItemPath.StartsWith(entry.Path, StringComparison.OrdinalIgnoreCase)) return new FilterResult(false);

			// check excludes
			return ExcludeMatches(entry, item.ItemPath, ID.Parse(item.ID), ID.Parse(item.TemplateID), item.TemplateName);
		}

		/// <summary>
		/// Checks if a preset includes a given set of criteria
		/// </summary>
		protected FilterResult Includes(IncludeEntry entry, string itemPath, ID itemId, ID templateId, string templateName, Database database)
		{
			// check for db match
			if (database.Name != entry.Database) return new FilterResult(false);

			// check for path match
			if (!itemPath.StartsWith(entry.Path, StringComparison.OrdinalIgnoreCase)) return new FilterResult(false);

			// check excludes
			return ExcludeMatches(entry, itemPath, itemId, templateId, templateName);
		}

		protected virtual FilterResult ExcludeMatches(IncludeEntry entry, string itemPath, ID itemId, ID templateId, string templateName)
		{
			FilterResult result = ExcludeMatchesPath(entry.Exclude, itemPath);

			if (!result.IsIncluded) return result;

			result = ExcludeMatchesTemplateId(entry.Exclude, templateId);

			if (!result.IsIncluded) return result;

			result = ExcludeMatchesTemplate(entry.Exclude, templateName);

			if (!result.IsIncluded) return result;

			result = ExcludeMatchesId(entry.Exclude, itemId);

			return result;
		}

		/// <summary>
		/// Checks if a given list of excludes matches a specific Serialization path
		/// </summary>
		protected virtual FilterResult ExcludeMatchesPath(IEnumerable<ExcludeEntry> entries, string sitecorePath)
		{
			bool match = entries.Any(entry => entry.Type.Equals("path", StringComparison.Ordinal) && sitecorePath.StartsWith(entry.Value, StringComparison.OrdinalIgnoreCase));

			return match
						? new FilterResult("Item path exclusion rule")
						: new FilterResult(true);
		}

		/// <summary>
		/// Checks if a given list of excludes matches a specific item ID. Use ID.ToString() format eg {A9F4...}
		/// </summary>
		protected virtual FilterResult ExcludeMatchesId(IEnumerable<ExcludeEntry> entries, ID id)
		{
			bool match = entries.Any(entry => entry.Type.Equals("id", StringComparison.Ordinal) && entry.Value.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase));

			return match
						? new FilterResult("Item ID exclusion rule")
						: new FilterResult(true);
		}

		/// <summary>
		/// Checks if a given list of excludes matches a specific template name
		/// </summary>
		protected virtual FilterResult ExcludeMatchesTemplate(IEnumerable<ExcludeEntry> entries, string templateName)
		{
			bool match = entries.Any(entry => entry.Type.Equals("template", StringComparison.Ordinal) && entry.Value.Equals(templateName, StringComparison.OrdinalIgnoreCase));

			return match
						? new FilterResult("Item template name exclusion rule")
						: new FilterResult(true);
		}

		/// <summary>
		/// Checks if a given list of excludes matches a specific template ID
		/// </summary>
		protected virtual FilterResult ExcludeMatchesTemplateId(IEnumerable<ExcludeEntry> entries, ID templateId)
		{
			bool match = entries.Any(entry => entry.Type.Equals("templateid", StringComparison.Ordinal) && entry.Value.Equals(templateId.ToString(), StringComparison.OrdinalIgnoreCase));

			return match
						? new FilterResult("Item template ID exclusion rule")
						: new FilterResult(true);
		}	
	}
}
