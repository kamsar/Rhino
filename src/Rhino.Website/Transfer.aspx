<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="Sitecore.Configuration" %>
<%@ Import Namespace="Sitecore.Data" %>
<%@ Import Namespace="Sitecore.Data.DataProviders" %>
<%@ Import Namespace="Sitecore.Data.Fields" %>
<%@ Import Namespace="Sitecore.Data.Items" %>
<%@ Import Namespace="Sitecore.Data.SqlServer" %>
<%@ Import Namespace="Sitecore.Globalization" %>
<%@ Import Namespace="Sitecore.SecurityModel" %>
<%@ Import Namespace="SitecoreData.DataProviders" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
    <head>
        <title>Transfer items between databases</title>
    </head>
    <body>
        <script runat="server">

            protected override void OnLoad(EventArgs e)
            {
                if (!IsPostBack)
                {
                    ddlSourceDatabase.DataSource = new object[] {"select source"}.Concat(Factory.GetDatabaseNames());
                    ddlSourceDatabase.DataBind();

                    ddlTargetDatabase.DataSource = new object[] {"select target"}.Concat(Factory.GetDatabaseNames());
                    ddlTargetDatabase.DataBind();
                }
            }

            private void OnStartButtonClick(object sender, EventArgs e)
            {
                if (!"select source".Equals(ddlSourceDatabase.SelectedValue) && !"select target".Equals(ddlTargetDatabase.SelectedValue))
                {
                    var sourceDatabase = Factory.GetDatabase(ddlSourceDatabase.SelectedValue);
                    var targetDatabase = Factory.GetDatabase(ddlTargetDatabase.SelectedValue);

                    if (sourceDatabase != null && targetDatabase != null)
                    {
                        using (new SecurityDisabler())
                        {
                            var item = sourceDatabase.GetRootItem();
                            var dataProvider = targetDatabase.GetDataProviders().First();

                            TransferRecursive(item, dataProvider);
                        }
                    }
                }
            }

            public void TransferRecursive(Item item, DataProvider provider)
            {
                Response.Write(string.Format("Transferring {0}<br />", item.Paths.FullPath));
                Response.Flush();

                ItemDefinition parentDefinition = null;

                if (item.Parent != null)
                {
                    parentDefinition = new ItemDefinition(item.Parent.ID, item.Parent.Name, item.Parent.TemplateID, item.Parent.BranchId);
                }

                // Create the item in database
                if (provider.CreateItem(item.ID, item.Name, item.TemplateID, parentDefinition, null))
                {
                    foreach (var language in item.Languages)
                    {
                        using (new LanguageSwitcher(language))
                        {
                            var itemInLanguage = item.Database.GetItem(item.ID);

                            if (itemInLanguage != null)
                            {
                                var itemDefinition = provider.GetItemDefinition(itemInLanguage.ID, null);

                                // Add all versions
                                foreach(var languageVersion in itemInLanguage.Versions.GetVersions(true))
                                {
                                    Response.Write(string.Format("&nbsp;&nbsp;Adding version {0} on language: {1}<br />", languageVersion.Version.Number, languageVersion.Language.Name));
             
                                    provider.AddVersion(itemDefinition, new VersionUri(language, languageVersion.Version), null);
                                }
                               
                                // Send the field values to the provider
                                var changes = new ItemChanges(itemInLanguage);

                                foreach (Field field in itemInLanguage.Fields)
                                {
                                    changes.FieldChanges[field.ID] = new FieldChange(field, field.Value);
                                }

                                provider.SaveItem(itemDefinition, changes, null);
                            }
                        }
                    }
                }

                if (!item.HasChildren)
                {
                    return;
                }

                foreach (Item child in item.Children)
                {
                    TransferRecursive(child, provider);
                }
            }
</script>
        <form runat="server">
            <p>
                Transfer items from  <asp:DropDownList runat="server" ID="ddlSourceDatabase" /> to <asp:DropDownList runat="server" ID="ddlTargetDatabase"  /> <asp:Button runat="server" ID="btnStart" OnClick="OnStartButtonClick" Text="Start" />
            </p>
        </form>
    </body>
</html>