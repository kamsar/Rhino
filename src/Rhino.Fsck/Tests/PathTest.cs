using System;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Serialization;

namespace Rhino.Fsck.Tests
{
	/// <summary>
	/// This test verifies that the path on disk matches the ItemPath in the serialized item. Note that part of the path is the database, so this also checks database validity.
	/// </summary>
	/// <remarks>
	/// Use the path constructor for serialization roots that include a database subdirectory (e.g. /master/sitecore) in their path
	/// Use the path + database constructor for roots that are already for a specific database (e.g. /sitecore)
	/// </remarks>
	public class PathTest : ITest
	{
		private readonly string _rootPath;
		private readonly string _databaseName;

		public PathTest(string rootPath)
			: this(rootPath, null)
		{

		}

		public PathTest(string rootPath, string databaseName)
		{
			_databaseName = databaseName;
			_rootPath = rootPath;
		}

		public string Name
		{
			get { return "Item Path Verification"; }
		}

		public TestResult Execute(DiskItem contextItem, DiskItem[] allItems)
		{
			// translate the full physical path into an item path
			var mappedPath = PathUtils.MakeItemPath(contextItem.FullPath, _rootPath);

			// if more than one item is in the same path with the same name, it will map something like "name_FDA63242325453" (guid)
			// we want to strip the disambiguating GUID from the name, if it exists
			var split = mappedPath.Split('_');
			if (ShortID.IsShortID(split.Last()))
				mappedPath = string.Join("_", split.Take(split.Length - 1));

			if (_databaseName != null)
			{
				string dbPrefix = "/" + _databaseName;

				if (mappedPath.StartsWith(dbPrefix, StringComparison.OrdinalIgnoreCase))
					mappedPath = mappedPath.Substring(dbPrefix.Length);
			}

			// MakeItemPath seems to return paths in the format "//sitecore/foo" sometimes, let's normalize that
			mappedPath = "/" + mappedPath.TrimStart('/');

			// if we have a database name (e.g. we are pointing at a raw serialized root such as "serialization\master" instead of "serialization"), prepend to the mapped path
			if (_databaseName != null)
			{
				mappedPath = "/" + _databaseName + mappedPath;
			}

			// compute the item reference path for the context item SyncItem
			string syncItemReferencePath = new ItemReference(contextItem.Item.DatabaseName, contextItem.Item.ItemPath).ToString();

			// ensure the ref path is prepended with /
			syncItemReferencePath = "/" + syncItemReferencePath.TrimStart('/');

			bool passed = mappedPath.Equals(syncItemReferencePath, StringComparison.OrdinalIgnoreCase);

			if (passed) return new TestResult(this, true);

			return new TestResult(this, false, string.Format("Physical: {0} != Serialized: {1}", mappedPath, syncItemReferencePath));
		}

	}
}
