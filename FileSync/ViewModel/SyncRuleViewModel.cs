namespace FileSync.ViewModel
{
    using System.Collections.Generic;
    using System.Linq;
    using FileSync.Model;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Messaging;

    public class SyncRuleViewModel : ViewModelBase
    {
        private readonly SyncRule _rule;

        private string _gitInfo;

        public SyncRuleViewModel(SyncRule rule)
        {
            _rule = rule;
        }

        public string Source
        {
            get { return _rule.Source; }
            set
            {
                _rule.Source = value;
                RaisePropertyChanged();
            }
        }

        public string Dest
        {
            get { return _rule.Dest; }
            set
            {
                _rule.Dest = value;
                RaisePropertyChanged();
            }
        }

        public bool Flatten
        {
            get { return _rule.Flatten; }
            set
            {
                _rule.Flatten = value;
                RaisePropertyChanged();
            }
        }

        public string Filters
        {
            get { return string.Join(",", _rule.Filters); }
            set
            {
                _rule.Filters = new List<string>(value.Split(','));
                RaisePropertyChanged();
            }
        }

        public bool Enabled
        {
            get { return _rule.Enabled; }
            set
            {
                if (_rule.Enabled != value)
                {
                    _rule.Enabled = value;
                    RaisePropertyChanged();
                    Messenger.Default.Send(new RuleEnabledMessage(this));
                }
            }
        }

        public string GitInfo
        {
            get { return _gitInfo; }
            set
            {
                if (_gitInfo != value)
                {
                    _gitInfo = value;
                    RaisePropertyChanged();
                }
            }
        }

        public SyncRule Rule => _rule;
    }
}