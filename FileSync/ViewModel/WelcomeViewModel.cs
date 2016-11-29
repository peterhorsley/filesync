namespace FileSync.ViewModel
{
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;

    public class WelcomeViewModel : ViewModelBase
    {
        public RelayCommand StartCommand => new RelayCommand(Start);

        private static void Start()
        {
            Messenger.Default.Send(Messages.AddRule);
        }
    }
}