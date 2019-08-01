namespace FileSync.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FileSync.Model;
    using FluentAssertions;
    using GalaSoft.MvvmLight.Messaging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    class SyncModelTests
    {
        private SyncSettings _settings;

        private List<string> _messages;

        private Mock<IMessenger> _messenger;

        private Mock<ISyncSettingsRepository> _repository;
        private string _testRootPath;
        private SyncModel _target;

        [SetUp]
        public void Setup()
        {
            _testRootPath = Path.Combine(Path.GetTempPath(), "FileSync-TestRun-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
            Directory.CreateDirectory(_testRootPath);

            _settings = new SyncSettings()
            {
                Rules = new List<SyncRule>(),
                ExcludedFileNameTokens = new List<string>(),
                ExcludedFilePathTokens = new List<string>()
            };

            _repository = new Mock<ISyncSettingsRepository>();

            _messages = new List<string>();
            _messenger = new Mock<IMessenger>();
            _messenger.Setup(m => m.Send<string>(It.IsAny<string>()))
                .Callback<string>(message => _messages.Add(message));
        }

        [TearDown]
        public void TearDown()
        {
            _target.Enable(false);
            if (!string.IsNullOrEmpty(_testRootPath) && Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        public void Rules_Loaded(int ruleCount)
        {
            // Arrange
            for (int i = 0; i < ruleCount; i++)
            {
                _settings.Rules.Add(CreateSyncRule(true, new List<string>()));
            }

            // Act
            CreateTarget(_settings);

            // Assert
            _target.Settings.Should().NotBeNull();
            _target.Settings.Rules.Should().NotBeNull().And.HaveCount(ruleCount);
            _target.Settings.ExcludedFilePathTokens.Should().NotBeNull().And.HaveCount(0);
            _target.Settings.ExcludedFileNameTokens.Should().NotBeNull().And.HaveCount(0);
        }

        [Test]
        public void Rules_Saved()
        {
            // Arrange
            _settings.Rules.Add(CreateSyncRule(true, new List<string>()));

            // Act
            CreateTarget(_settings);
            _settings.ExcludedFileNameTokens.Add("test");
            _target.Save();

            // Assert
            _repository.Verify(m => m.Save(_settings), Times.Once);
        }

        [Test]
        public void WhenEnabled_AndDestinationEmpty_SyncsAll_Flattened()
        {
            // Arrange
            var rule = CreateSyncRule(true, new List<string>());
            _settings.Rules.Add(rule);
            var fileName = "a.txt";
            var fileContent = "abc";
            Directory.CreateDirectory(rule.Source);
            File.WriteAllText(Path.Combine(rule.Source, fileName), fileContent);
            CreateTarget(_settings);

            // Act
            _target.Enable(true);
            WaitForOneSyncCycle();
            _messages.Clear();

            // Assert
            var destFilePath = Path.Combine(rule.Dest, fileName);
            File.Exists(destFilePath).Should().BeTrue();
            File.ReadAllText(destFilePath).Should().Be(fileContent);
        }

        /*
        [TestCase(@"c:\source\file.exe")]
        [TestCase(@"c:\source\1\file.exe")]
        [TestCase(@"c:\source\1\2\file.exe")]
        [TestCase(@"c:\source\1\2\file.EXE")]
        [TestCase(@"c:\source\1\2\file.dll")]
        [TestCase(@"c:\source\1\2\3\file.dll")]
        public void WhenEnabled_AndMatchingFileChanged_SyncsFile_Flattened(string filepath)
        {
            // Arrange
            _settings.Rules.Add(CreateFlattenSyncRule());
            _directory.Setup(m => m.GetFiles(_settings.Rules[0].Source, "*.*", SearchOption.AllDirectories)).Returns(new [] { filepath });
            _fileInfoFactory.Setup(m => m.Create(filepath)).Returns(new FileInfoWrap())
            _file.Setup(m => m.Exists(filepath)).Returns(true);
            var target = CreateTarget(_settings);

            // Act
            target.Enable(true);
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Created += null, new FileSystemEventArgs(
                WatcherChangeTypes.Created, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Created += null, new FileSystemEventArgs(
                WatcherChangeTypes.Created, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
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
            WaitForOneSyncCycle();
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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(filepath), Path.GetFileName(filepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
            _messages.Clear();

            watchers[0].Raise(w => w.Changed += null, new FileSystemEventArgs(
                WatcherChangeTypes.Changed, Path.GetDirectoryName(sourceFilepath), Path.GetFileName(sourceFilepath)));

            WaitForOneSyncCycle();

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
            WaitForOneSyncCycle();
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
            WaitForOneSyncCycle();

            // Assert
            _directory.Verify(m => m.CreateDirectory(enabledDefinition.Dest));
            _directory.Verify(m => m.CreateDirectory(disabledDefinition.Dest), Times.Never);
        }
        */

        private void WaitForOneSyncCycle()
        {
            int millisecondsToWait = 5000;
            int millisecondsWaited = 0;
            int checkIntervalMs = 100;
            while (!_messages.Contains(Messages.StopSync))
            {
                Thread.Sleep(checkIntervalMs);
                millisecondsWaited += checkIntervalMs;
                millisecondsWaited.Should().BeLessThan(millisecondsToWait);
            }
        }

        private SyncRule CreateSyncRule(bool flatten, List<string> filters)
        {
            return new SyncRule()
            {
                Enabled = true,
                Dest = Path.Combine(_testRootPath, "dest"),
                Source = Path.Combine(_testRootPath, "source"),
                Filters = filters,
                Flatten = flatten
            };
        }

        private void CreateTarget(SyncSettings settings)
        {
            _repository.Setup(m => m.Load()).Returns(settings);

            _target = new SyncModel(
                _repository.Object,
                _messenger.Object);
        }
    }
}