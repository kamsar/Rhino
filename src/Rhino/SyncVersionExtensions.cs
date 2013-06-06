using Sitecore.Data.Serialization.ObjectModel;
using Sitecore.Diagnostics;

namespace Rhino
{
	internal static class SyncVersionExtensions
	{
		public static SyncVersion Clone(this SyncVersion version)
		{
			Assert.ArgumentNotNull(version, "version");

			var newSyncVersion = new SyncVersion
			{
				Language = version.Language,
				Revision = version.Revision,
				Version = version.Version
			};

			foreach (var field in version.Fields)
			{
				newSyncVersion.AddField(field.FieldID, field.FieldName, field.FieldKey, field.FieldValue, true);
			}

			return newSyncVersion;
		}
	}
}
