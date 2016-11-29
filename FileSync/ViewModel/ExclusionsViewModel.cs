namespace FileSync.ViewModel
{
    using System;
    using System.IO;
    using System.Linq;
    using FileSync.Model;
    using FileSync.View;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;

    public class ExclusionsViewModel : ViewModelBase
    {
        private readonly SyncModel _model;

        public RelayCommand OkCommand => new RelayCommand(Ok);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);

        public ExclusionsViewModel(SyncModel model)
        {
            _model = model;
            FileNameExclusions = string.Join(Environment.NewLine, model.Settings.ExcludedFileNameTokens);
            FilePathExclusions = string.Join(Environment.NewLine, model.Settings.ExcludedFilePathTokens);
        }

        public bool CancelAvailable => !App.FirstRun;

        public string OkText => App.FirstRun ? "Done" : "Ok";

        private void Cancel()
        {
            ViewNavigation.Direction = Direction.Backward;
            Messenger.Default.Send(Messages.ShowSync);
        }

        private void Ok()
        {
            _model.Settings.ExcludedFileNameTokens.Clear();
            _model.Settings.ExcludedFilePathTokens.Clear();
            _model.Settings.ExcludedFileNameTokens.AddRange(FileNameExclusions.Split(new [] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            _model.Settings.ExcludedFilePathTokens.AddRange(FilePathExclusions.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            _model.Save();

            ViewNavigation.Direction = Direction.Forward;
            Messenger.Default.Send(Messages.ShowSync);
        }

        public string FileNameExclusions { get; set; }
        public string FilePathExclusions { get; set; }
    }
}