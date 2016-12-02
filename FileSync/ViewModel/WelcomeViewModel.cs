namespace FileSync.ViewModel
{
    using System;
    using System.Linq;
    using System.Reflection;
    using FileSync.Model;
    using FileSync.View;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;

    public class WelcomeViewModel : ViewModelBase
    {
        private readonly SyncModel _model;

        public WelcomeViewModel(SyncModel model)
        {
            _model = model;
            ButtonText = model.Settings.Rules.Any() ? "OK" : "Get started";
        }

        public string ButtonText { get; set; }

        public RelayCommand StartCommand => new RelayCommand(Start);

        private void Start()
        {
            if (_model.Settings.Rules.Any())
            {
                ViewNavigation.Direction = Direction.Forward;
                Messenger.Default.Send(Messages.ShowSync);
            }
            else
            {
                ViewNavigation.Direction = Direction.Forward;
                Messenger.Default.Send(Messages.AddRule);
            }
        }

        public string Version => App.Version;
    }
}