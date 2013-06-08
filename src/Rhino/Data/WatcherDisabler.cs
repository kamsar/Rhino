using System;
using System.IO;

namespace Rhino.Data
{
	internal class WatcherDisabler : IDisposable
	{
		private readonly FileSystemWatcher _fileSystemWatcher;

		public WatcherDisabler(FileSystemWatcher fileSystemWatcher)
		{
			_fileSystemWatcher = fileSystemWatcher;
			_fileSystemWatcher.EnableRaisingEvents = false;
		}

		public void Dispose()
		{
			_fileSystemWatcher.EnableRaisingEvents = true;
		}
	}
}
