namespace Mejnun.Platform
{
    public interface ISaveStorage
    {
        bool FileExists(string file);
        bool KeyExists(string key, string file);
        void Save<T>(string key, T value, string file);
        T Load<T>(string key, string file);
        T Load<T>(string key, string file, T defaultValue);
        void LoadInto<T>(string key, string file, T obj) where T : class;
        void DeleteKey(string key, string file);
        void DeleteFile(string file);
    }
}