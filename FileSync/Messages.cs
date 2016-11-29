namespace FileSync
{
    using FileSync.Model;
    using ViewModel;

    public static class Messages
    {
        public const string ShowSync = "ShowSync";
        public const string StartSync = "StartSync";
        public const string StopSync = "StopSync";
        public const string AddRule = "AddRule";
        public const string ShowExclusions = "ShowExclusions";
        public const string ShowWelcome = "ShowWelcome";
        public const string SyncActive = "SyncActive";
        public const string SyncInactive = "SyncInactive";
    }

    public class RuleEnabledMessage
    {
        public RuleEnabledMessage(SyncRuleViewModel syncRule)
        {
            SyncRule = syncRule;
        }

        public SyncRuleViewModel SyncRule;
    }

    public class EditRuleMessage
    {
        public EditRuleMessage(SyncRule syncRule)
        {
            SyncRule = syncRule;
        }

        public SyncRule SyncRule;
    }

    public class LogMessage
    {
        public LogMessage(string text)
        {
            Text = text;
        }

        public string Text;
    }
}
