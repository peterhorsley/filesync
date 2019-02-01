namespace FileSync.Model
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SystemInterface.IO;
    using SystemWrapper.IO;
    using GalaSoft.MvvmLight.Messaging;

    public class SyncModel
    {
        private bool _enabled;

        private readonly Dictionary<IFileSystemWatcher, SyncRule> _watchers =
            new Dictionary<IFileSystemWatcher, SyncRule>();

        private readonly object _watcherLock = new object();

        private readonly ISyncSettingsRepository _settingsRepository;

        private readonly IFile _file;

        private readonly IDirectory _directory;

        private readonly IFileSystemWatcherFactory _watcherFactory;

        private readonly IFileInfoFactory _infoFactory;

        private readonly SyncSettings _settings;

        private readonly IMessenger _messenger;

        public SyncModel(ISyncSettingsRepository settingsRepository)
            : this(
                settingsRepository,
                Messenger.Default,
                new FileWrap(),
                new DirectoryWrap(),
                new FileSystemWatcherFactory(),
                new FileInfoFactory())
        {
        }

        public SyncModel(
            ISyncSettingsRepository settingsRepository,
            IMessenger messenger,
            IFile file, 
            IDirectory directory,
            IFileSystemWatcherFactory watcherFactory,
            IFileInfoFactory infoFactory)
        {
            _settingsRepository = settingsRepository;
            _file = file;
            _directory = directory;
            _watcherFactory = watcherFactory;
            _infoFactory = infoFactory;
            _messenger = messenger;
            _settings = _settingsRepository.Load();
        }

        public SyncSettings Settings => _settings;

        private void CreateWatcher(SyncRule rule, string filter)
        {
            var watcher = _watcherFactory.Create(rule.Source);
            watcher.EnableRaisingEvents = true;
            watcher.Filter = filter;
            watcher.IncludeSubdirectories = true;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;
            _watchers[watcher] = rule;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!_file.Exists(e.FullPath))
            {
                return;
            }

            // Look up the original watcher - we have to check both the wrapped file system watcher and
            // the internal concrete instance unfortunately, to support mocking this via tests.
            var key = _watchers.Keys.First(w => w == sender || w.FileSystemWatcherInstance == sender);

            var dest = GetFileDest(e.FullPath, _watchers[key].Source, _watchers[key].Dest, _watchers[key].Flatten);

            if (ShouldSync(e.FullPath, dest))
            {
				// Give the writing process a little breathing room to reduce the chances of file-in-use issues.
                Thread.Sleep(500);
                  
                _messenger.Send(Messages.StartSync);
                _messenger.Send(new LogMessage(string.Format("Synchronizing {0} to {1} ({2})", e.FullPath, dest, e.ChangeType)));
                CopyFile(e.FullPath, dest);
                _messenger.Send(Messages.StopSync);
            }
        }

        private string GetFileDest(string sourceFilePath, string sourceFolder, string destFolder, bool flatten)
        {
            var dest = "";
            if (flatten)
            {
                dest = Path.Combine(destFolder, Path.GetFileName(sourceFilePath));
            }
            else
            {
                var relativeSourcePath = sourceFilePath.Substring(sourceFolder.Length).TrimStart('\\');
                dest = Path.Combine(destFolder, relativeSourcePath);
            }

            return dest;
        }

        private bool ShouldSync(string sourcePath, string destPath)
        {
            var lowerCasePath = sourcePath.ToLowerInvariant();
            var lowerCaseFileName = Path.GetFileName(lowerCasePath);

            if (_settings.ExcludedFileNameTokens.Any(token => lowerCaseFileName.Contains(token)))
            {
                return false;
            }

            if (_settings.ExcludedFilePathTokens.Any(token => lowerCasePath.Contains(token)))
            {
                return false;
            }

            if (!_file.Exists(destPath))
            {
                return true;
            }

            try
            {
                var destInfo = _infoFactory.Create(destPath);
                var sourceInfo = _infoFactory.Create(sourcePath);
                if (destInfo.Length == sourceInfo.Length &&
                    destInfo.LastWriteTimeUtc.Ticks == sourceInfo.LastWriteTimeUtc.Ticks)
                {
                    return false;
                }
            }
            catch (FileNotFoundException ex)
            {
                if (ex.FileName.ToLowerInvariant().Equals(destPath.ToLowerInvariant()))
                {
                    // Destination file has just been deleted, proceed to sync.
                    return true;
                }

                // Source file has just been deleted, no need to sync.
                return false;
            }

            return true;
        }

        public void Enable(bool value)
        {
            if (_settings == null)
            {
                throw new InvalidOperationException("Call Use() to apply settings before calling Enable().");
            }

            if (_enabled == value)
            {
                return;
            }

            _enabled = value;

            if (_enabled)
            {
                if (!_settings.Rules.Any())
                {
                    throw new InvalidOperationException("No sync rules specified.");
                }

                Task.Run(
                    () =>
                    {
                        _messenger.Send(Messages.StartSync);

                        foreach (var rule in EnabledRules)
                        {
                            foreach (var filter in rule.Filters)
                            {
                                if (!CopyFilesWithFilter(rule, filter))
                                {
                                    InitialCopyFailed?.Invoke(this, new EventArgs());
                                    _enabled = false;
                                }

                                if (!_enabled)
                                {
                                    break;
                                }
                            }

                            if (!_enabled)
                            {
                                break;
                            }
                        }

                        if (_enabled)
                        {
                            lock (_watcherLock)
                            {
                                if (_enabled)
                                {
                                    foreach (var rule in EnabledRules)
                                    {
                                        _messenger.Send(new LogMessage(string.Format("Watching {0}", rule.Source)));

                                        foreach (var filter in rule.Filters)
                                        {
                                            CreateWatcher(rule, filter);
                                        }
                                    }
                                }
                            }
                        }

                        _messenger.Send(Messages.StopSync);
                    });
            }
            else
            {
                DestroyWatchers();
            }
        }

        public event EventHandler InitialCopyFailed;

        private IEnumerable<SyncRule> EnabledRules
        {
            get { return _settings.Rules.Where(s => s.Enabled); }
        }

        private void DestroyWatchers()
        {
            lock (_watcherLock)
            {
                foreach (var watcher in _watchers.Keys)
                {
                    watcher.Changed -= Watcher_Changed;
                    watcher.Created -= Watcher_Changed;
                    watcher.Renamed -= Watcher_Changed;
                }
                _watchers.Clear();
            }
        }

        private bool CopyFilesWithFilter(SyncRule rule, string filter)
        {
            try
            {
                _directory.CreateDirectory(rule.Dest);
            }
            catch (Exception ex)
            {
                _messenger.Send(new LogMessage(string.Format("Failed to create directory {0}: {1}", rule.Dest, ex.Message)));
                return false;
            }
            
            foreach (var sourceFile in _directory.GetFiles(rule.Source, filter, SearchOption.AllDirectories))
            {
                if (!_enabled)
                {
                    break;
                }

                var destPath = GetFileDest(sourceFile, rule.Source, rule.Dest, rule.Flatten);
                if (ShouldSync(sourceFile, destPath))
                {
                    _messenger.Send(new LogMessage(string.Format("Copying {0} to {1}", sourceFile, destPath)));
                    CopyFile(sourceFile, destPath);
                }
            }

            return true;
        }

        private void CopyFile(string sourceFile, string dest)
        {
            var copied = false;
            var attempts = 0;
            while (!copied)
            {
                try
                {
                    var destFolder = Path.GetDirectoryName(dest);
                    if (!_directory.Exists(destFolder))
                    {
                        _directory.CreateDirectory(destFolder);
                    }
                    _file.Copy(sourceFile, dest, true);
                    copied = true;
                }
                catch (Exception ex)
                {
                    Thread.Sleep(500);
                    attempts++;
                    if (attempts >= 10)
                    {
                        _messenger.Send(
                            new LogMessage(string.Format("Failed to copy {0} to {1}: {2}", sourceFile, dest, ex.Message)));
                        break;
                    }
                }
            }
        }

        public void Save()
        {
            _settingsRepository.Save(_settings);
        }
    }
}
