namespace FileSync.Model
{
    public interface ISyncSettingsRepository
    {
        SyncSettings Load();

        void Save(SyncSettings settings);
    }
}