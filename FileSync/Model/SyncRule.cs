namespace FileSync.Model
{
    using System.Collections.Generic;

    public class SyncRule
    {
        public string Dest;

        public string Source;

        public List<string> Filters;

        public bool Enabled;

        public bool Flatten;
    }
}