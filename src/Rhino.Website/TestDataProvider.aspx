<%-- 
    MongoDB DataProvider Sitecore module
    Copyright (C) 2012  Robin Hermanussen

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
--%>
<%@ Page Language="C#" AutoEventWireup="true" %>
<%@ Import Namespace="Sitecore" %>
<%@ Import Namespace="Sitecore.Caching" %>
<%@ Import Namespace="Sitecore.Configuration" %>
<%@ Import Namespace="Sitecore.Data" %>
<%@ Import Namespace="Sitecore.Data.DataProviders" %>
<%@ Import Namespace="Sitecore.Data.Fields" %>
<%@ Import Namespace="Sitecore.Data.Items" %>
<%@ Import Namespace="Sitecore.SecurityModel" %>
<%@ Import Namespace="Sitecore.Data.Managers" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
    <head>
        <title>Test some actions on data providers</title>
    </head>
    <body>

        <script runat="server">

            protected override void OnLoad(EventArgs e)
            {
                if (!IsPostBack)
                {
                    var dataProviders = new List<DataProvider>();
                    
                    foreach (var databaseName in Factory.GetDatabaseNames())
                    {
                        dataProviders.AddRange(Factory.GetDatabase(databaseName).GetDataProviders());
                    }
                    
                    rptDataProviders.DataSource = dataProviders;
                    rptDataProviders.DataBind();
                }
                else
                {
                    RunTest();
                }
            }

            private void RunTest()
            {
                Response.Write(string.Format("<p>Starting test at {0}</p>", DateTime.Now));
                Response.Flush();

                foreach (RepeaterItem rptItem in rptDataProviders.Items)
                {
                    var includeProvider = rptItem.FindControl("cbxIncludeProvider") as CheckBox;
                    if (includeProvider.Checked)
                    {
                        var startItemId = rptItem.FindControl("tbxStartItemId") as TextBox;
                        var providerInfo = includeProvider.Text.Split(new[] {':'});
                        var databaseName = providerInfo[0];
                        var providerTypeName = providerInfo[1].Trim();
                        foreach (
                            var dataProvider in
                                Factory.GetDatabase(databaseName).GetDataProviders().Where(provider => providerTypeName.Equals(provider.GetType().FullName)))
                        {
                            var callContext = new CallContext(dataProvider.Database.DataManager, rptDataProviders.Items.Count);
                            var startItem = dataProvider.GetItemDefinition(new ID(startItemId.Text), callContext);

                            if (startItem == null)
                            {
                                Response.Write(string.Format("<p>Startitem {0} was not found with provider {1}</p>", startItemId, includeProvider.Text));
                            }
                            else
                            {
                                var originalValue = dataProvider.CacheOptions.DisableAll;
                                dataProvider.CacheOptions.DisableAll = true;
                                using (new DatabaseCacheDisabler())
                                {
                                    RunTest(dataProvider, startItem, callContext);
                                }
                                dataProvider.CacheOptions.DisableAll = originalValue;
                            }
                        }
                    }
                }
            }

            private void RunTest(DataProvider dataProvider, ItemDefinition startItem, CallContext callContext)
            {
                Response.Write(string.Format("<p>Starting test with provider {0}: {1}</p>", dataProvider.Database.Name, dataProvider.GetType().FullName));
                Response.Flush();

                var createdItems = new Dictionary<ID, string>();
                var createdChildItems = new Dictionary<ID, string>();

                Response.Write("<table><tr><th>Action</th><th>Duration (in milliseconds)</th></tr>");

                CacheManager.ClearAllCaches();

                // Create a whole bunch of items
                var startTime = DateTime.Now;
                for (var i = 0; i < 50; i++)
                {
                    var id = Sitecore.Data.ID.NewID;
                    var name = string.Format("Test item {0}", i);
                    createdItems.Add(id, name);
                    dataProvider.CreateItem(id, name, TemplateIDs.Folder, startItem, callContext);

                    for (var j = 0; j < 50; j++)
                    {
                        var childId = Sitecore.Data.ID.NewID;
                        var childName = string.Format("Test child item {0} {1}", i, j);
                        createdChildItems.Add(childId, childName);
                        dataProvider.CreateItem(childId, childName, TemplateIDs.Folder, new ItemDefinition(id, name, TemplateIDs.Folder, Sitecore.Data.ID.Null),
                                                callContext);
                    }
                }
                Response.Write(string.Format("<tr><td>Create</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Add versions to the items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.AddVersion(itemDef, new VersionUri(LanguageManager.GetLanguage("en"), new Sitecore.Data.Version(0)), callContext);
                    dataProvider.AddVersion(itemDef, new VersionUri(LanguageManager.GetLanguage("en"), new Sitecore.Data.Version(1)), callContext);
                    dataProvider.AddVersion(itemDef, new VersionUri(LanguageManager.GetLanguage("en"), new Sitecore.Data.Version(2)), callContext);
                }
                Response.Write(string.Format("<tr><td>Add versions</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                // Retrieve all items the normal way because we need them for the following actions (no performance measurement needed)
                var retrievedItems = new Dictionary<ID, Item>();
                using (new SecurityDisabler())
                {
                    foreach (var item in createdItems.Concat(createdChildItems))
                    {
                        retrievedItems.Add(item.Key, dataProvider.Database.GetItem(item.Key));
                    }
                }

                CacheManager.ClearAllCaches();

                // Save the items with some changed field values
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    var changes = new ItemChanges(retrievedItems[item.Key]);

                    changes.FieldChanges[FieldIDs.Created] = new FieldChange(new Field(FieldIDs.Created, retrievedItems[item.Key]),
                                                                             "some value");
                    changes.FieldChanges[FieldIDs.CreatedBy] = new FieldChange(new Field(FieldIDs.CreatedBy, retrievedItems[item.Key]),
                                                                               "some value");
                    changes.FieldChanges[FieldIDs.Updated] = new FieldChange(new Field(FieldIDs.Updated, retrievedItems[item.Key]),
                                                                             "some value");
                    changes.FieldChanges[FieldIDs.UpdatedBy] = new FieldChange(new Field(FieldIDs.UpdatedBy, retrievedItems[item.Key]),
                                                                               "some value");
                    changes.FieldChanges[FieldIDs.Owner] = new FieldChange(new Field(FieldIDs.Owner, retrievedItems[item.Key]), "some value");
                    changes.FieldChanges[FieldIDs.Originator] = new FieldChange(new Field(FieldIDs.Originator, retrievedItems[item.Key]),
                                                                                "some value");
                    changes.FieldChanges[FieldIDs.DisplayName] = new FieldChange(new Field(FieldIDs.DisplayName, retrievedItems[item.Key]),
                                                                                 "some value");

                    dataProvider.SaveItem(itemDef, changes, callContext);
                }
                Response.Write(string.Format("<tr><td>Change some field values</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Get the parent id's for the items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.GetParentID(itemDef, callContext);
                }
                Response.Write(string.Format("<tr><td>Get parent id's</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Get the child id's for the items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.GetChildIDs(itemDef, callContext);
                }
                Response.Write(string.Format("<tr><td>Get child id's</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Get item definitions for the items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    dataProvider.GetItemDefinition(item.Key, callContext);
                }
                Response.Write(string.Format("<tr><td>Get item definitions</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Get the versions for the items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.GetItemVersions(itemDef, callContext);
                }
                Response.Write(string.Format("<tr><td>Get item versions</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Get field values for the items
                var vu = new VersionUri(LanguageManager.GetLanguage("en"), new Sitecore.Data.Version(1));
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems))
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.GetItemFields(itemDef, vu, callContext);
                }
                Response.Write(string.Format("<tr><td>Get field values</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                CacheManager.ClearAllCaches();

                // Delete all the created items
                startTime = DateTime.Now;
                foreach (var item in createdItems.Concat(createdChildItems).Reverse())
                {
                    var itemDef = new ItemDefinition(item.Key, item.Value, TemplateIDs.Folder, Sitecore.Data.ID.Null);
                    dataProvider.DeleteItem(itemDef, callContext);
                }
                Response.Write(string.Format("<tr><td>Delete</td><td>{0}</td></tr>", DateTime.Now.Subtract(startTime).TotalMilliseconds));
                Response.Flush();

                Response.Write("</table>");
            }

</script>
        <form runat="server">

            <asp:Repeater runat="server" ID="rptDataProviders">
                <ItemTemplate>
                    <asp:CheckBox runat="server" ID="cbxIncludeProvider" Text='<%# string.Format("{0}: {1}", ((DataProvider)Container.DataItem).Database.Name, Container.DataItem.GetType().FullName) %>' /><br/>
                    Start item: <asp:TextBox runat="server" ID="tbxStartItemId" Text="{0DE95AE4-41AB-4D01-9EB0-67441B7C2450}" /><!-- {0FCC62A1-CDF9-4D04-BA9D-3ACCDC4A5F9D} -->
                </ItemTemplate>
                <SeparatorTemplate>
                    <br />
                </SeparatorTemplate>
            </asp:Repeater>

            <p><asp:Button runat="server" Text="Run test" /></p>
        </form>
    </body>
</html>