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
    using Microsoft.WindowsAPICodePack.Dialogs;

    public class EditRuleViewModel : ViewModelBase
    {
        private readonly SyncModel _model;

        private readonly SyncRule _rule;

        private readonly bool _isNew;

        public RelayCommand SaveCommand => new RelayCommand(SaveRule);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);

        public RelayCommand BrowseForSourceFolderCommand => new RelayCommand(BrowseForSourceFolder);
        public RelayCommand BrowseForDestFolderCommand => new RelayCommand(BrowseForDestFolder);

        public string SaveButtonText => _isNew ? "Add" : "Save";

        public string TitleActionText => _isNew ? "Add Rule" : "Edit Rule";

        private void BrowseForSourceFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                Title = "Choose source folder",
                InitialDirectory = Source,
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Source = dialog.FileName;
            }
        }

        private void BrowseForDestFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                Title = "Choose destination folder",
                InitialDirectory = Destination,
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Destination = dialog.FileName;
            }
        }

        public EditRuleViewModel(SyncModel model)
            : this(model, new SyncRule())
        {
        }

        public EditRuleViewModel(SyncModel model, SyncRule rule)
        {
            _model = model;
            _rule = rule;
            _isNew = !RuleValid;
        }

        public bool CancelAvailable => !App.FirstRun;

        private void Cancel()
        {
            ViewNavigation.Direction = Direction.Backward;
            Messenger.Default.Send(_model.Settings.Rules.Any() ? Messages.ShowSync : Messages.ShowWelcome);
        }

        private void SaveRule()
        {
            _rule.Dest = _rule.Dest.ToLowerInvariant();
            _rule.Source = _rule.Source.ToLowerInvariant();

            if (!_rule.Filters.Any())
            {
                _rule.Filters.Add("*.*");
            }

            if (_isNew)
            {
                _model.Settings.Rules.Add(_rule);
            }
            
            _model.Save();

            ViewNavigation.Direction = Direction.Forward;
            Messenger.Default.Send(App.FirstRun ? Messages.ShowExclusions : Messages.ShowSync);
        }

        public string Source
        {
            get { return _rule.Source; }
            set
            {
                _rule.Source = value; 
                RaisePropertyChanged();
                RaisePropertyChanged("SourceValid");
                RaisePropertyChanged("RuleValid");
            }
        }

        public string Destination
        {
            get { return _rule.Dest; }
            set
            {
                _rule.Dest = value;
                RaisePropertyChanged();
                RaisePropertyChanged("DestinationValid");
                RaisePropertyChanged("RuleValid");
            }
        }

        public string Filters
        {
            get { return string.Join(",", _rule.Filters); }
            set
            {
                _rule.Filters =
                    value.Replace(";", ",").Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
                RaisePropertyChanged("FiltersValid");
                RaisePropertyChanged("RuleValid");
            }
        }

        public bool Flatten
        {
            get { return _rule.Flatten; }
            set { _rule.Flatten = value; }
        }

        public bool SourceValid => !string.IsNullOrWhiteSpace(Source) && Directory.Exists(Source);

        public bool DestinationValid => !string.IsNullOrWhiteSpace(Destination) && Directory.Exists(Destination);

        public bool FiltersValid
        {
            get
            {
                return string.IsNullOrWhiteSpace(Filters) || Filters.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries).All(filter => !string.IsNullOrWhiteSpace(filter));
            }
        }

        public bool RuleValid => SourceValid && DestinationValid && FiltersValid;
    }
}
