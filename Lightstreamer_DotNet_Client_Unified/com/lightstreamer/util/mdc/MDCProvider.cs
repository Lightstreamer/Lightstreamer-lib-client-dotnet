namespace com.lightstreamer.util.mdc
{
    /// <summary>
    /// A MDC provider is basically a key-value map storing context information for the sake of loggers.
    /// </summary>
    public interface MDCProvider
    {

        void put(string key, string value);
        string get(string key);
        void remove(string key);
        void clear();
    }
}