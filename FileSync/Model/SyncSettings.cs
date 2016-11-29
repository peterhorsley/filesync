namespace FileSync.Model
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    public class SyncSettings
    {
        public List<SyncRule> Rules;

        public List<string> ExcludedFileNameTokens;

        public List<string> ExcludedFilePathTokens;
    }
}
