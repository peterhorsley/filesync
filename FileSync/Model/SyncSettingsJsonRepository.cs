namespace FileSync.Model
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    public class SyncSettingsJsonRepository : ISyncSettingsRepository
    {
        private readonly string _filePath;

        public SyncSettingsJsonRepository(string filePath)
        {
            _filePath = filePath;
        }

        public SyncSettings Load()
        {
            SyncSettings settings = new SyncSettings();
            if (File.Exists(_filePath))
            {
                try
                {
                    settings = JsonConvert.DeserializeObject<SyncSettings>(File.ReadAllText(_filePath));
                }
                catch (Exception)
                {
                    // If we failed to parse the file, just start fresh.  
                }
            }

            if (settings.Rules == null)
            {
                settings.Rules = new List<SyncRule>();
            }

            if (settings.ExcludedFileNameTokens == null)
            {
                settings.ExcludedFileNameTokens = new List<string>();
            }

            if (settings.ExcludedFilePathTokens == null)
            {
                settings.ExcludedFilePathTokens = new List<string>();
            }

            foreach (var rule in settings.Rules)
            {
                // Reset enabled flag on load.
                rule.Enabled = false;

                // Ensure at least one inclusion filter is specified.
                if (!rule.Filters.Any())
                {
                    rule.Filters.Add("*.*");
                }
            }

            return settings;
        }

        public void Save(SyncSettings settings)
        {
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(settings));
        }

    }
}