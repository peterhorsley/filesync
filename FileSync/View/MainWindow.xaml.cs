using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Shell;
using FileSync.Model;
using FileSync.ViewModel;
using GalaSoft.MvvmLight.Messaging;

namespace FileSync.View
{
    /// <summary>
    /// Interaction logic for SyncView.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private readonly Timer _progressTimer;

        private bool _syncFinished;

        private int _minTaskbarAnimationTimeMs = 3000;

        private SyncModel _syncModel;

        private SyncViewModel _syncViewModel;

        private string _settingsPath;

        private readonly string _titlebarText;

        private const string TitleBarActiveText = " - Active";

        public MainWindow()
        {
            InitializeComponent();
            _titlebarText = Title;
            _progressTimer = new Timer(Callback);
            Messenger.Default.Register<string>(this, OnMessageReceived);
            Messenger.Default.Register<EditRuleMessage>(this, OnEditRule);
        }

        private void OnEditRule(EditRuleMessage message)
        {
            ShowEditRuleView(message.SyncRule);
        }

        private void Callback(object state)
        {
            if (_syncFinished)
            {
                _progressTimer.Change(Timeout.Infinite, 1000);
                Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
                    });
            }
        }

        private void OnMessageReceived(string message)
        {
            Application.Current.Dispatcher.Invoke(
                () =>
                {
                    switch (message)
                    {
                        case Messages.SyncActive:
                            Title = _titlebarText + TitleBarActiveText;
                            break;
                        case Messages.SyncInactive:
                            Title = _titlebarText;
                            break;
                        case Messages.StartSync:
                            _syncFinished = false;
                            StartTitleBarAnimationThread();
                            TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
                            _progressTimer.Change(_minTaskbarAnimationTimeMs, 1000);
                            break;
                        case Messages.StopSync:
                            _syncFinished = true;
                            break;
                        case Messages.AddRule:
                            ShowAddRuleView();
                            break;
                        case Messages.ShowExclusions:
                            ShowExclusionsView();
                            break;
                        case Messages.ShowWelcome:
                            ShowWelcomeView();
                            break;
                        case Messages.ShowSync:
                            ShowSyncView();
                            break;
                    }
                });
        }

        private void StartTitleBarAnimationThread()
        {
            new Thread(TitleBarAnimationThread).Start();
        }

        private void TitleBarAnimationThread()
        {
            try
            {
                while (!_syncFinished)
                {
                    Application.Current.Dispatcher.Invoke(() => Title = _titlebarText + TitleBarActiveText + " /");
                    Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() => Title = _titlebarText + TitleBarActiveText + " -");
                    Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() => Title = _titlebarText + TitleBarActiveText + " \\");
                    Thread.Sleep(100);
                    Application.Current.Dispatcher.Invoke(() => Title = _titlebarText + TitleBarActiveText + " |");
                    Thread.Sleep(100);
                }

                Application.Current.Dispatcher.Invoke(() => Title = _titlebarText + TitleBarActiveText);
            }
            catch
            {
                if (App.Exiting)
                {
                    return; // just exit
                }
                throw;
            }
        }

        private void ShowExclusionsView()
        {
            View.Content = new ExclusionsView() {DataContext = new ExclusionsViewModel(_syncModel)};
        }

        private void ShowAddRuleView()
        {
            View.Content = new EditRuleView() {DataContext = new EditRuleViewModel(_syncModel) };
        }

        private void ShowEditRuleView(SyncRule rule)
        {
            View.Content = new EditRuleView() { DataContext = new EditRuleViewModel(_syncModel, rule) };
        }

        public void Present(string settingsPath)
        {
            _settingsPath = settingsPath;
            _syncModel = CreateSyncModel();
            if (_syncModel.Settings.Rules.Any())
            {
                ShowSyncView();
            }
            else
            {
                ShowWelcomeView();
            }
            
            Show();
        }

        private SyncModel CreateSyncModel()
        {
            return new SyncModel(new SyncSettingsJsonRepository(_settingsPath));
        }

        private void ShowWelcomeView()
        {
            App.FirstRun = true;
            View.Content = new WelcomeView() {DataContext = new WelcomeViewModel(_syncModel) };
        }

        private void ShowSyncView()
        {
            App.FirstRun = false;
            _syncModel = CreateSyncModel();
            _syncViewModel = new SyncViewModel(_syncModel);
            View.Content = new SyncView() { DataContext = _syncViewModel };
        }

        public void Dispose()
        {
            Messenger.Default.Unregister<string>(this, OnMessageReceived);
            Messenger.Default.Unregister<EditRuleMessage>(this, OnEditRule);
            _syncViewModel?.Dispose();
            _syncModel?.Enable(false);
        }
    }
}
