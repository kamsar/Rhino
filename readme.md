# Rhino

Experimental [Sitecore](http://www.sitecore.net) data provider that loads serialized items directly from disk.

Why would you want to do this? Well, imagine storing your templates and renderings as serialized items with _no syncing tools_ (such as [Unicorn](https://github.com/kamsar/Unicorn) or [TDS](http://www.hhogdev.com/Products/Team-Development-for-Sitecore/Overview.aspx)).

The data provider simply ghosts in the serialized items into the content tree, making them appear as normal items.

## News ##

* Read/write provider implemented: Supports adding and removing items and versions, copying, moving, etc. Properties, template changes, shared field migrations are not currently supported.
* High speed indexed in-memory serialization store. Should scale to expected number of items for templates, etc. Won't scale to a large content database, but that's not the intended purpose.

## TODO ##

* Presently the provider indexes an entire database. The goal is to make it include only parts of the database (e.g. act as a data provider in concert with the SQL provider on the master database)
* Cache invalidation with a FileSystemWatcher (when serialized items change, invalidate the cache for them). This is handled if the change is made by Rhino, but not if say SCM updates change files on disk.
* Ability to exclude certain portions of an included path, ala a serialization preset
* Option to 'auto-load' an included path: if the serialization root path for an inclusion is empty, fill it using the SQL provider's set of items. This would make installation really easy.
* Testing. Make no mistake this is an alpha quality codebase. I wouldn't use this in production right now.

## Performance ##

The provider loads the entire set of serialized items into RAM at startup and uses lazy indexes to further increase performance. Obviously this can use significant RAM if you make the database too large.
However it's very nice for performance once it is loaded - significantly faster than the SQL data provider at reads, but slower at writes due to the amount of disk access involved.

## Notes ##

This is a highly modified fork of [SitecoreData](https://github.com/pbering/SitecoreData), a project that has an embryonic serialization data provider.

Rhino would have never happened if [@alexshyba](https://twitter.com/alexshyba) hadn't suggested the idea and brought the SitecoreData project to my attention.

This is built with Sitecore 7.0 130424

## Installation ##

* Clone
* Place Sitecore.Kernel.dll in \lib\Sitecore\
* Grab the 'website' folder from a Sitecore zip distribution and copy the contents into Rhino.Website (git will ignore them)
* Serialize the master database
* Rename the 'master' serialization folder 'nosqlserialization'
* Open the admin and switch to the nosqlserialization database
* Break things

## Contributing ##

Please do :)