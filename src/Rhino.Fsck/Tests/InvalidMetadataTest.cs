using Sitecore;
using Sitecore.Data;

namespace Rhino.Fsck.Tests
{
	public class InvalidMetadataTest : ITest
	{
		public string Name
		{
			get { return "Missing Item Metadata"; }
		}

		public TestResult Execute(DiskItem contextItem, DiskItem[] allItems)
		{
			if(!ID.IsID(contextItem.Item.ID))
				return new TestResult(this, false, (contextItem.Item.ID ?? "null") + " is not a valid item ID.");

			if(ID.Parse(contextItem.Item.ID) == ID.Null)
				return new TestResult(this, false, "Item ID was the null ID.");

			if (!ID.IsID(contextItem.Item.ParentID))
				return new TestResult(this, false, (contextItem.Item.ParentID ?? "null") + " is not a valid parent ID.");

			if (ID.Parse(contextItem.Item.ParentID) == ID.Null && contextItem.Item.ID != ItemIDs.RootID.ToString())
				return new TestResult(this, false, "Parent ID was the null ID.");

			if (!ID.IsID(contextItem.Item.TemplateID))
				return new TestResult(this, false, (contextItem.Item.TemplateID ?? "null") + " is not a valid template ID.");

			if (ID.Parse(contextItem.Item.TemplateID) == ID.Null)
				return new TestResult(this, false, "Template ID was the null ID.");

			if (!ID.IsID(contextItem.Item.MasterID))
				return new TestResult(this, false, (contextItem.Item.MasterID ?? "null") + " is not a valid master ID.");

			if(string.IsNullOrWhiteSpace(contextItem.Item.TemplateName))
				return new TestResult(this, false, "Template name was null or empty.");

			if (string.IsNullOrWhiteSpace(contextItem.Item.ItemPath))
				return new TestResult(this, false, "Path was null or empty.");

			if (string.IsNullOrWhiteSpace(contextItem.Item.DatabaseName))
				return new TestResult(this, false, "Database was null or empty.");

			if (string.IsNullOrWhiteSpace(contextItem.Item.Name))
				return new TestResult(this, false, "Item name was null or empty.");

			if (contextItem.Item.SharedFields.Count == 0)
			{
				if (contextItem.Item.Versions.Count == 0 || contextItem.Item.Versions[0].Fields.Count == 0)
					return new TestResult(this, false, "Item had no shared fields and no versioned fields. While this can be valid, it is highly unusual.");
			}

			return new TestResult(this, true);
		}
	}
}
