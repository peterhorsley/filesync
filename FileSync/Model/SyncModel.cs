using System.Text.RegularExpressions;

namespace FileSync.Model
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using GalaSoft.MvvmLight.Messaging;

    public class SyncModel
    {
        private bool _enabled;
        private readonly ISyncSettingsRepository _settingsRepository;
        private readonly SyncSettings _settings;
        private readonly IMessenger _messenger;

        public SyncModel(ISyncSettingsRepository settingsRepository)
            : this(settingsRepository, Messenger.Default)
        {
        }

        public SyncModel(
            ISyncSettingsRepository settingsRepository,
            IMessenger messenger)
        {
            _settingsRepository = settingsRepository;
            _messenger = messenger;
            _settings = _settingsRepository.Load();
        }

        public SyncSettings Settings => _settings;

        private void SyncFile(SyncRule rule, string sourceFilePath)
        {
            var dest = GetFileDest(sourceFilePath, rule.Source, rule.Dest, rule.Flatten);
            if (ShouldSync(rule, sourceFilePath, dest))
            {
                _messenger.Send(Messages.StartCopy);
                _messenger.Send(new LogMessage(string.Format("Synchronizing {0} to {1}", sourceFilePath, dest)));
                CopyFile(sourceFilePath, dest);
                _messenger.Send(Messages.StopCopy);
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

        private bool ShouldSync(SyncRule rule, string sourcePath, string destPath)
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

            var fileName = Path.GetFileName(sourcePath);
            var matchesFilter = false;
            foreach (var filter in rule.Filters)
            {
                if (FitsMask(fileName, filter))
                {
                    matchesFilter = true;
                    break;
                }
            }

            if (!matchesFilter)
            {
                return false;
            }

            if (!File.Exists(destPath))
            {
                return true;
            }

            try
            {
                var destInfo = new FileInfo(destPath);
                var sourceInfo = new FileInfo(sourcePath);
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

                new Thread(WatchThread).Start();
            }
        }

        private IEnumerable<SyncRule> EnabledRules
        {
            get { return _settings.Rules.Where(s => s.Enabled); }
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
                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }
                    File.Copy(sourceFile, dest, true);
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

        private bool FitsMask(string fileName, string fileMask)
        {
            Regex mask = new Regex(fileMask.Replace(".", "[.]").Replace("*", ".*").Replace("?", "."));
            return mask.IsMatch(fileName);
        }

        public void Save()
        {
            _settingsRepository.Save(_settings);
        }

        private void WatchThread()
        {
            while (_enabled)
            {
                _messenger.Send(Messages.StartSync);
                foreach (var rule in EnabledRules)
                {
                    _messenger.Send(new LogMessage(string.Format("Watching {0}", rule.Source)));
                    try
                    {
                        if (Directory.Exists(rule.Source))
                        {
                            var files = Directory.GetFiles(rule.Source, "*.*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                if (!_enabled)
                                {
                                    break;
                                }
                                SyncFile(rule, file);
                            }
                        }
                    }
                    catch {
                        // Ignore errors dealing with files, as we will try again later.
                    }
                    Thread.Sleep(1000);
                }
                _messenger.Send(Messages.StopSync);
            }
            _messenger.Send(new LogMessage(""));
        }
    }
}
