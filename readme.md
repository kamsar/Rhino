# Rhino

Experimental [Sitecore](http://www.sitecore.net) data provider that loads serialized items directly from disk.

Why would you want to do this? Well, imagine storing your templates and renderings as serialized items with _no syncing tools_ (such as [Unicorn](https://github.com/kamsar/Unicorn) or [TDS](http://www.hhogdev.com/Products/Team-Development-for-Sitecore/Overview.aspx)).

The data provider simply ghosts in the serialized items into the content tree, making them appear as normal items.

## News ##

* Renaming items now works correctly, including migration of child items to their new path (note: renaming an item with LOTS of children would probably be pretty slow since we have to rewrite the path on each one)
* Watcher support. If you change, rename, or delete a serialized item on disk appropriate caches get cleared so the change appears immediately in the content editor (eg you pulled some updated items from Git)
* Support for filtering the data provider's scope. For example, you attach the provider to the master database and specify that you only want /sitecore/templates to be handled by it
    * Supports exclusions, leading to a "jagged" tree (e.g Templates is Rhino, but Templates/system is not)
    * Inclusions/exclusions are managed by the serialization preset system built into sitecore. This makes it easy to do an initial serialization with /sitecore/admin/serialization.aspx
    * Rhino doesn't even really need an initial serialization, as the SQL provider still merges children with it. For example, if Templates/Foo is included, but not serialized yet, and you change it those changes will be written to disk and then start "overriding" the SQL version of the item.
* Improved serialized file reading (performance ~1.7x faster loading cache on the first site load on my SSD)
* Read/write provider implemented: Supports adding and removing items and versions, copying, moving, etc. Properties, template changes, shared field migrations are not currently supported.
* High speed indexed in-memory serialization store. Should scale to expected number of items for templates, etc. Won't scale to a large content database, but that's not the intended purpose.

## TODO ##
* Test with a production-like configuration that might also use items from the core db (with a different instance of the provider). Do we need to point directly to the serialized root and assume a db name in path calcs? Reject items based on other db path? Interop when multiple DBs are serialized in part.
* Option to 'auto-load' an included path: if the serialization root path for an inclusion is empty, fill it using the SQL provider's set of items. This would make installation really easy.
* Testing, preferably of a repeatable automated nature. Make no mistake this is an alpha quality codebase. I wouldn't use this in production right now. The fsck tool should help with this.
* Implement "fsck" for serialized folders. Read all the items in, check that paths and databases in the files match physical paths, parent IDs match (if the parent is serialized), no duplicate IDs in different files, tree consistency (eg if /foo is serialized, /foo/bar is not, but /foo/bar/baz is = probable error), ???

## Performance ##

The provider loads the entire set of serialized items into RAM at startup and uses lazy indexes to further increase performance. Obviously this can use significant RAM if you make the database too large.
However it's very nice for performance once it is loaded - significantly faster than the SQL data provider at reads, but slower at writes due to the amount of disk access involved.

## Notes ##

### Rhino would have never happened if [@alexshyba](https://twitter.com/alexshyba) hadn't suggested the idea to me and brought the SitecoreData project to my attention. He's the man. I just wrote some code :)

This is a highly modified fork of [SitecoreData](https://github.com/pbering/SitecoreData), a project that has an embryonic serialization data provider.

[Robin Hermanussen](https://twitter.com/knifecore) also has written a similar provider to this but more geared towards usage as a unit testing tool (i.e. it supports blobs, but not item renaming): [Fixture](https://github.com/hermanussen/Sitecore-FixtureDataProvider)

This is built with Sitecore 7.0 130424

## Installation ##

* Clone
* Place Sitecore.Kernel.dll in \lib\Sitecore\
* Grab the 'website' folder from a Sitecore zip distribution and copy the contents into Rhino.Website (git will ignore them)
* Configure the Serialization Preset in Rhino.config to include the things you want in Rhino
* Visit /sitecore/admin/serialization.aspx and "serialize preconfigured" to serialize the appropriate items to disk for Rhino to read
* Open the Content Editor and start messing with items in the Rhino managed sections - if all goes well you should see them modified on disk as changes are made
* Break things (and report them as issues!)

## Rhino? ##

Quite a silly moniker don't you agree? Chosen because a Rhino, much like a [Unicorn](https://github.com/kamsar/Unicorn), has a prominent horn. Hey, it could have been Narwhal :)

## Contributing ##

Please do. Pull requests, feature requests, and forking are encouraged :)