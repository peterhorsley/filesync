namespace FileSync.ViewModel
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using FileSync.Model;
    using FileSync.View;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;
    using LibGit2Sharp;

    public class SyncViewModel : ViewModelBase
    {
        private readonly SyncModel _model;

        private bool _syncActive;

        private string _logText;

        private string _syncButtonText;

        private readonly Timer _gitInfoTimer;

        private readonly object _ruleCollectionLock = new object();

        public SyncViewModel(SyncModel model)
        {
            _model = model;
            _model.InitialCopyFailed += OnInitialCopyFailed;
            Rules = new ObservableCollection<SyncRuleViewModel>();
            foreach (var rule in _model.Settings.Rules)
            {
                Rules.Add(new SyncRuleViewModel(rule));
            }

            Messenger.Default.Register<LogMessage>(this, OnLogMessageReceived);
            Messenger.Default.Register<RuleEnabledMessage>(this, OnRuleEnabled);
            UpdateSyncButtonText();
            _gitInfoTimer = new Timer(OnGitInfoTimer);
            _gitInfoTimer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromMilliseconds(-1));
        }

        private void OnInitialCopyFailed(object sender, EventArgs eventArgs)
        {
            SyncActive = false;
        }

        public RelayCommand<SyncRuleViewModel> EditRuleCommand => new RelayCommand<SyncRuleViewModel>(EditRule, (r) => SyncInactive);
        public RelayCommand<SyncRuleViewModel> DeleteRuleCommand => new RelayCommand<SyncRuleViewModel>(DeleteRule, (r) => SyncInactive);
        public RelayCommand<SyncRuleViewModel> ToggleRuleEnabledCommand => new RelayCommand<SyncRuleViewModel>(r => r.Enabled = !r.Enabled, (r) => SyncInactive);

        private void EditRule(SyncRuleViewModel ruleViewModel)
        {
            Messenger.Default.Send<EditRuleMessage>(new EditRuleMessage(ruleViewModel.Rule));
        }

        private void DeleteRule(SyncRuleViewModel ruleViewModel)
        {
            lock (_ruleCollectionLock)
            {
                _model.Settings.Rules.Remove(ruleViewModel.Rule);
                _model.Save();
                var index = Rules.IndexOf(ruleViewModel);
                Rules.RemoveAt(index);
                if (Rules.Count == 0)
                {
                    ViewNavigation.Direction = Direction.Backward;
                    Messenger.Default.Send(Messages.ShowWelcome);
                }
            }
        }

        public RelayCommand AddRuleCommand => new RelayCommand(
            () =>
            {
                ViewNavigation.Direction = Direction.Forward;
                Messenger.Default.Send(Messages.AddRule);
            }, () => SyncInactive);

        public RelayCommand ExclusionsCommand => new RelayCommand(
            () =>
            {
                ViewNavigation.Direction = Direction.Forward;
                Messenger.Default.Send(Messages.ShowExclusions);
            }, () => SyncInactive);

        public RelayCommand AboutCommand => new RelayCommand(
            () =>
            {
                ViewNavigation.Direction = Direction.Backward;
                Messenger.Default.Send(Messages.ShowWelcome);
            }, () => SyncInactive);

        private void OnGitInfoTimer(object state)
        {
            StopGitTimer();
            lock (_ruleCollectionLock)
            {
                foreach (var rule in Rules)
                {
                    var gitInfo = GetGitInfo(rule.Source);
                    if (Application.Current != null)
                    {
                        try
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                rule.GitInfo = gitInfo;
                            });
                        }
                        catch
                        {
                            // Can happen on app exit, just ignore.
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
            StartGitTimer();
        }

        private void StopGitTimer()
        {
            _gitInfoTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void StartGitTimer()
        {
            _gitInfoTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(-1));
        }

        private string GetGitInfo(string path)
        {
            try
            {
                if (Repository.IsValid(path))
                {
                    using (var repo = new Repository(path))
                    {
                        return "On branch " + repo.Head.FriendlyName;
                    }
                }

            }
            catch (LibGit2SharpException)
            {
                // Ignore, could be file in use.
            }

            return "";
        }

        private void OnRuleEnabled(RuleEnabledMessage rule)
        {
            RaisePropertyChanged("RuleSelected");
            RaisePropertyChanged("NoRulesSelected");
        }

        private void OnLogMessageReceived(LogMessage message)
        {
            LogText = string.Format("[{1}] {2}{0}{3}", Environment.NewLine, DateTime.Now, message.Text, LogText);
        }

        public string LogText
        {
            get { return _logText; }
            set
            {
                _logText = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<SyncRuleViewModel> Rules { get; }

        public bool SyncActive
        {
            get { return _syncActive; }
            set
            {
                if (_syncActive != value)
                {
                    _syncActive = value;
                    _model.Enable(value);
                    UpdateSyncButtonText();
                    RaisePropertyChanged();
                    RaisePropertyChanged("SyncInactive");
                    Messenger.Default.Send(_syncActive ? Messages.SyncActive : Messages.SyncInactive);
                }
            }
        }

        public bool SyncInactive => !SyncActive;

        private void UpdateSyncButtonText()
        {
            SyncButtonText = SyncActive ? "Stop" : "Sync";
        }

        public string SyncButtonText
        {
            get { return _syncButtonText; }
            set
            {
                _syncButtonText = value; 
                RaisePropertyChanged();
            }
        }

        public bool NoRulesSelected => !RuleSelected;

        public bool RuleSelected
        {
            get
            {
                return Rules.Any(s => s.Enabled);
            }
        }

        public void Dispose()
        {
            StopGitTimer();
            _model.InitialCopyFailed -= OnInitialCopyFailed;
        }
    }
}
