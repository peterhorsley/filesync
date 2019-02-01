namespace FileSync.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SystemInterface;
    using SystemInterface.IO;
    using FileSync.Model;
    using FluentAssertions;
    using GalaSoft.MvvmLight.Messaging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    class SyncModelTests
    {
        private Mock<IFile> _file;

        private Mock<IDirectory> _directory;

        private Mock<IFileSystemWatcherFactory> _watcherFactory;

        private Mock<IFileInfoFactory> _fileInfoFactory;

        private SyncSettings _settings;

        private List<string> _messages;

        private Mock<IMessenger> _messenger;

        private Mock<ISyncSettingsRepository> _repository;

        [SetUp]
        public void Setup()
        {
            _settings = new SyncSettings()
            {
                Rules = new List<SyncRule>(),
                ExcludedFileNameTokens = new List<string>(),
                ExcludedFilePathTokens = new List<string>()
            };

            _file = new Mock<IFile>();
            _directory = new Mock<IDirectory>();
            _watcherFactory = new Mock<IFileSystemWatcherFactory>();
            _fileInfoFactory = new Mock<IFileInfoFactory>();
            _repository = new Mock<ISyncSettingsRepository>();

            _messages = new List<string>();
            _messenger = new Mock<IMessenger>();
            _messenger.Setup(m => m.Send<string>(It.IsAny<string>()))
                .Callback<string>(message => _messages.Add(message));
        }

        [TestCase(1)]
        [TestCase(10)]
        public void Rules_Loaded(int ruleCount)
        {
            // Arrange
            for (int i = 0; i < ruleCount; i++)
            {
                _settings.Rules.Add(CreateFlattenSyncRule());
            }

            // Act
            var target = CreateTarget(_settings);

            // Assert
            target.Settings.Should().NotBeNull();
            target.Settings.Rules.Should().NotBeNull().And.HaveCount(ruleCount);
            target.Settings.ExcludedFilePathTokens.Should().NotBeNull().And.HaveCount(0);
            target.Settings.ExcludedFileNameTokens.Should().NotBeNull().And.HaveCount(0);
        }

        [Test]
        public void Rules_Saved()
        {
            // Arrange
            _settings.Rules.Add(CreateFlattenSyncRule());

            // Act
            var target = CreateTarget(_settings);
            _settings.ExcludedFileNameTokens.Add("test");
            target.Save();

            // Assert
            _repository.Verify(m => m.Save(_settings), Times.Once);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        public void WhenDisabledDuringInitialSync_AbortsFileCopy_AndNoWatchersCreated(int numberOfRules)
        {
            // Arrange
            for (int i = 0; i < numberOfRules; i++)
            {
                _settings.Rules.Add(CreateFlattenSyncRule());
            }

            // Simulate some time for file copy
            int fileCount = 10000;
            int getFilesCalls = 0;
            int maxGetFilesCalls = 0;
            _file.Setup(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), true)).Callback(() => { Task.Delay(500); });
            foreach (var definition in _settings.Rules)
            {
                foreach (var filter in definition.Filters)
                {
                    maxGetFilesCalls++;
                    _directory.Setup(m => m.GetFiles(definition.Source, filter, SearchOption.AllDirectories))
                        .Returns(Enumerable.Range(0, fileCount).Select(i => i.ToString()).ToArray()).Callback(
                            () =>
                            {
                                getFilesCalls++;
                                Task.Delay(500);
                            });
                }
            }

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            target.Enable(false);
            WaitForSyncStartThenStop();

            // Assert
            getFilesCalls.Should().BeGreaterThan(0).And.BeLessThan(maxGetFilesCalls);
            _watcherFactory.Verify(m => m.Create(It.IsAny<string>()), Times.Never);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        public void WhenEnabled_PerformsInitialSync_AndCreatesWatchers(int numberOfRules)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            for (var i = 0; i < numberOfRules; i++)
            {
                var definitionIndex = i;
                _settings.Rules.Add(CreateFlattenSyncRule());
                _watcherFactory.Setup(m => m.Create(_settings.Rules[definitionIndex].Source))
                    .Returns(() =>
                        {
                            var watcher = new Mock<IFileSystemWatcher>();
                            watchers.Add(watcher);
                            return watcher.Object;
                        });
            }

            // Mock file copy
            int fileCount = 3;
            int getFilesCalls = 0;
            int expectedGetFilesCalls = 0;
            _file.Setup(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), true)).Callback(() => { Task.Delay(10); });
            foreach (var definition in _settings.Rules)
            {
                foreach (var filter in definition.Filters)
                {
                    expectedGetFilesCalls++;
                    _directory.Setup(m => m.GetFiles(definition.Source, filter, SearchOption.AllDirectories))
                        .Returns(Enumerable.Range(0, fileCount).Select(i => i.ToString()).ToArray()).Callback(
                        () => getFilesCalls++);
                }
            }

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();

            // Assert
            getFilesCalls.Should().Be(expectedGetFilesCalls);

            int watcherIndex = 0;
            int expectedWatcherCount = 0;
            foreach (var definition in _settings.Rules)
            {
                foreach (var filter in definition.Filters)
                {
                    expectedWatcherCount++;
                    expectedWatcherCount.Should().BeLessOrEqualTo(watchers.Count);
                    watchers[watcherIndex].VerifySet(m => m.EnableRaisingEvents = true);
                    watchers[watcherIndex].VerifySet(m => m.IncludeSubdirectories = true);
                    watchers[watcherIndex].VerifySet(m => m.Filter = filter);
                    watcherIndex++;
                }
            }
        }

        [TestCase(@"c:\source\file.exe")]
        [TestCase(@"c:\source\1\file.exe")]
        [TestCase(@"c:\source\1\2\file.exe")]
        [TestCase(@"c:\source\1\2\file.EXE")]
        [TestCase(@"c:\source\1\2\file.dll")]
        [TestCase(@"c:\source\1\2\3\file.dll")]
        public void WhenEnabled_AndMatchingFileChanged_SyncsFile_Flattened(string filepath)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(filepath, Path.Combine(_settings.Rules[0].Dest, Path.GetFileName(filepath)), true), Times.Once);
        }

        [TestCase(@"c:\source\file.exe")]
        [TestCase(@"c:\source\1\file.exe")]
        [TestCase(@"c:\source\1\2\file.exe")]
        [TestCase(@"c:\source\1\2\file.EXE")]
        [TestCase(@"c:\source\1\2\file.dll")]
        [TestCase(@"c:\source\1\2\3\file.dll")]
        public void WhenEnabled_AndMatchingFileChanged_SyncsFile_NonFlattened(string filepath)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateNonFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            var expectedDestFilePath = filepath.Replace(_settings.Rules[0].Source, _settings.Rules[0].Dest);
            _file.Verify(m => m.Copy(filepath, expectedDestFilePath, true), Times.Once);
        }

        [TestCase(@"c:\source\file.exe")]
        [TestCase(@"c:\source\1\file.exe")]
        [TestCase(@"c:\source\1\2\file.exe")]
        [TestCase(@"c:\source\1\2\file.EXE")]
        [TestCase(@"c:\source\1\2\file.dll")]
        [TestCase(@"c:\source\1\2\3\file.dll")]
        public void WhenEnabled_AndMatchingFileCreated_SyncsFile_Flattened(string filepath)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Created += null, new FileSystemEventArgs(
                WatcherChangeTypes.Created, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(filepath, Path.Combine(_settings.Rules[0].Dest, Path.GetFileName(filepath)), true), Times.Once);
        }

        [TestCase(@"c:\source\file.exe")]
        [TestCase(@"c:\source\1\file.exe")]
        [TestCase(@"c:\source\1\2\file.exe")]
        [TestCase(@"c:\source\1\2\file.EXE")]
        [TestCase(@"c:\source\1\2\file.dll")]
        [TestCase(@"c:\source\1\2\3\file.dll")]
        public void WhenEnabled_AndMatchingFileCreated_SyncsFile_NonFlattened(string filepath)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateNonFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Created += null, new FileSystemEventArgs(
                WatcherChangeTypes.Created, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            var expectedDestFilePath = filepath.Replace(_settings.Rules[0].Source, _settings.Rules[0].Dest);
            _file.Verify(m => m.Copy(filepath, expectedDestFilePath, true), Times.Once);
        }

        [TestCase(@"c:\source\file.exe", "file")]
        [TestCase(@"c:\source\1\file.exe", "file.")]
        [TestCase(@"c:\source\1\2\file.exe", "e.exe")]
        [TestCase(@"c:\source\1\2\file.EXE", "e.exe")]
        [TestCase(@"c:\source\1\2\file.dll", "file.dll")]
        public void WhenEnabled_AndFileWithExclusionTokenInNameChanged_DoesNotSyncFile(string filepath, string filenameExclusionToken)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _settings.ExcludedFileNameTokens = new List<string> {filenameExclusionToken};
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            // Assert
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            _messages.Should().HaveCount(0);
        }

        [TestCase(@"c:\source\file.exe", "source")]
        [TestCase(@"c:\source\1\file.exe", "1")]
        [TestCase(@"c:\source\1\2\file.exe", "1\\2")]
        [TestCase(@"c:\source\1\2\file.EXE", "file.exe")]
        [TestCase(@"c:\source\1\2\file.dll", @"c:\source\1\2\file.dll")]
        public void WhenEnabled_AndFileWithExclusionTokenInPathChanged_DoesNotSyncFile(string filepath, string filepathExclusionToken)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _settings.ExcludedFilePathTokens = new List<string> { filepathExclusionToken };
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            // Assert
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            _messages.Should().HaveCount(0);
        }

        [TestCase(@"c:\source\file.exe", "test")]
        [TestCase(@"c:\source\1\file.exe", "1\\exclude.exe")]
        [TestCase(@"c:\source\1\2\file.exe", "3")]
        [TestCase(@"c:\source\1\2\file.EXE", "file.exe.config")]
        [TestCase(@"c:\source\1\2\file.dll", @"c:\source\1\2\file.dll.orig")]
        public void WhenEnabled_AndFileWithoutExclusionTokenInPathChanged_SyncsFile(string filepath, string filepathExclusionToken)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _settings.ExcludedFilePathTokens = new List<string> { filepathExclusionToken };
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(filepath, Path.Combine(_settings.Rules[0].Dest, Path.GetFileName(filepath)), true), Times.Once);
        }

        [TestCase(@"c:\source\file.exe", "file1.exe")]
        [TestCase(@"c:\source\1\file.exe", "_file.exe")]
        [TestCase(@"c:\source\1\2\file.exe", "other")]
        [TestCase(@"c:\source\1\2\file.EXE", "file.exe.config")]
        public void WhenEnabled_AndFileWithoutExclusionTokenInNameChanged_SyncsFile(string filepath, string filenameExclusionToken)
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _settings.ExcludedFileNameTokens = new List<string> { filenameExclusionToken };
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(filepath, Path.Combine(_settings.Rules[0].Dest, Path.GetFileName(filepath)), true), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndDestinationFileExistWithSameSizeAndTimestamp_DoesNotSyncFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);
            
            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            var length = 10;
            var timestamp = new Mock<IDateTime>();
            timestamp.SetupGet(m => m.Ticks).Returns(100);

            var sourceFileInfo = new Mock<IFileInfo>();
            sourceFileInfo.SetupGet(m => m.Length).Returns(length);
            sourceFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp.Object);

            var destFileInfo = new Mock<IFileInfo>();
            destFileInfo.SetupGet(m => m.Length).Returns(length);
            destFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp.Object);

            _fileInfoFactory.Setup(m => m.Create(sourceFilepath)).Returns(sourceFileInfo.Object);
            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Returns(destFileInfo.Object);

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            // Assert
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            _messages.Should().HaveCount(0);
        }

        [Test]
        public void WhenEnabled_AndDestinationFileExistWithSameSizeAndDifferentTimestamp_SyncsFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);

            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            var length = 10;
            var timestamp1 = new Mock<IDateTime>();
            timestamp1.SetupGet(m => m.Ticks).Returns(100);
            var timestamp2 = new Mock<IDateTime>();
            timestamp2.SetupGet(m => m.Ticks).Returns(200);

            var sourceFileInfo = new Mock<IFileInfo>();
            sourceFileInfo.SetupGet(m => m.Length).Returns(length);
            sourceFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp1.Object);

            var destFileInfo = new Mock<IFileInfo>();
            destFileInfo.SetupGet(m => m.Length).Returns(length);
            destFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp2.Object);

            _fileInfoFactory.Setup(m => m.Create(sourceFilepath)).Returns(sourceFileInfo.Object);
            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Returns(destFileInfo.Object);

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(sourceFilepath, destFilepath, true), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndDestinationFileExistWithDifferentSizeAndDifferentTimestamp_SyncsFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);

            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            var timestamp1 = new Mock<IDateTime>();
            timestamp1.SetupGet(m => m.Ticks).Returns(100);
            var timestamp2 = new Mock<IDateTime>();
            timestamp2.SetupGet(m => m.Ticks).Returns(200);

            var sourceFileInfo = new Mock<IFileInfo>();
            sourceFileInfo.SetupGet(m => m.Length).Returns(10);
            sourceFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp1.Object);

            var destFileInfo = new Mock<IFileInfo>();
            destFileInfo.SetupGet(m => m.Length).Returns(11);
            destFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp2.Object);

            _fileInfoFactory.Setup(m => m.Create(sourceFilepath)).Returns(sourceFileInfo.Object);
            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Returns(destFileInfo.Object);

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(sourceFilepath, destFilepath, true), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndDestinationFileExistWithDifferentSizeAndSameTimestamp_SyncsFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);

            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            var timestamp = new Mock<IDateTime>();
            timestamp.SetupGet(m => m.Ticks).Returns(100);

            var sourceFileInfo = new Mock<IFileInfo>();
            sourceFileInfo.SetupGet(m => m.Length).Returns(10);
            sourceFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp.Object);

            var destFileInfo = new Mock<IFileInfo>();
            destFileInfo.SetupGet(m => m.Length).Returns(11);
            destFileInfo.SetupGet(m => m.LastWriteTimeUtc).Returns(timestamp.Object);

            _fileInfoFactory.Setup(m => m.Create(sourceFilepath)).Returns(sourceFileInfo.Object);
            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Returns(destFileInfo.Object);

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(sourceFilepath, destFilepath, true), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndDestinationFileIsDeletedShortlyAfterBeingCreated_SyncsFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);

            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Throws(new FileNotFoundException("", destFilepath));

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForSyncStartThenStop();

            // Assert
            _file.Verify(m => m.Copy(sourceFilepath, destFilepath, true), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndSourceFileIsDeletedShortlyAfterBeingCreated_DoesNotSyncFile()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            _settings.Rules.Add(CreateFlattenSyncRule());
            _watcherFactory.Setup(m => m.Create(_settings.Rules[0].Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var filename = "file.exe";
            var sourceFilepath = Path.Combine(_settings.Rules[0].Source, filename);
            var destFilepath = Path.Combine(_settings.Rules[0].Dest, filename);

            _file.Setup(m => m.Exists(sourceFilepath)).Returns(true);
            _file.Setup(m => m.Exists(destFilepath)).Returns(true);

            var destFileInfo = new Mock<IFileInfo>();
            _fileInfoFactory.Setup(m => m.Create(destFilepath)).Returns(destFileInfo.Object);
            _fileInfoFactory.Setup(m => m.Create(sourceFilepath)).Throws(new FileNotFoundException("", sourceFilepath));

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            // Assert
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _file.Verify(m => m.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            _messages.Should().HaveCount(0);
        }

        [Test]
        public void WhenEnabled_AndEnabledDestFoldersDoNotExist_CreatesThem()
        {
            // Arrange
            var watchers = new List<Mock<IFileSystemWatcher>>();
            var enabledDefinition = CreateFlattenSyncRule();
            var disabledDefinition = CreateFlattenSyncRule();
            disabledDefinition.Enabled = false;
            disabledDefinition.Dest = @"d:\some\disabled\dest";
            _settings.Rules.AddRange(new [] { enabledDefinition, disabledDefinition });

            _watcherFactory.Setup(m => m.Create(enabledDefinition.Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            _watcherFactory.Setup(m => m.Create(disabledDefinition.Source))
                .Returns(() =>
                {
                    var watcher = new Mock<IFileSystemWatcher>();
                    watchers.Add(watcher);
                    return watcher.Object;
                });

            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForSyncStartThenStop();

            // Assert
            _directory.Verify(m => m.CreateDirectory(enabledDefinition.Dest));
            _directory.Verify(m => m.CreateDirectory(disabledDefinition.Dest), Times.Never);
        }

        private void WaitForSyncStartThenStop()
        {
            int millisecondsToWait = 2000;
            int millisecondsWaited = 0;
            int checkIntervalMs = 100;
            while (!_messages.Contains(Messages.StartSync) || !_messages.Contains(Messages.StopSync) || _messages.Count < 2)
            {
                Thread.Sleep(checkIntervalMs);
                millisecondsWaited += checkIntervalMs;
                millisecondsWaited.Should().BeLessThan(millisecondsToWait);
            }

            _messages.First().ShouldBeEquivalentTo(Messages.StartSync);
            _messages.Last().ShouldBeEquivalentTo(Messages.StopSync);
        }

        private SyncRule CreateFlattenSyncRule()
        {
            return CreateSyncRule(true);
        }

        private SyncRule CreateNonFlattenSyncRule()
        {
            return CreateSyncRule(false);
        }

        private SyncRule CreateSyncRule(bool flatten)
        {
            return new SyncRule()
            {
                Enabled = true,
                Dest = @"c:\dest",
                Source = @"c:\source",
                Filters = new List<string>() { "*.exe", "*.dll" },
                Flatten = flatten
            };
        }

        private SyncModel CreateTarget(SyncSettings settings)
        {
            _repository.Setup(m => m.Load()).Returns(settings);

            var target = new SyncModel(
                _repository.Object,
                _messenger.Object,
                _file.Object,
                _directory.Object,
                _watcherFactory.Object,
                _fileInfoFactory.Object);

            return target;
        }
    }
}