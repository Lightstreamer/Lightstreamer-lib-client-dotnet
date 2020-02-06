namespace com.lightstreamer.util.mdc
{
    //using MDC = org.apache.log4j.MDC;

    /// <summary>
    /// A MDC provider relaying on Log4J.
    /// </summary>
    public class Log4JMDCProvider : MDCProvider
    {

        public virtual void put(string key, string value)
        {
            MDC.put(key, value);
        }

        public virtual string get(string key)
        {
            return (string)MDC.get(key);
        }

        public virtual void remove(string key)
        {
            MDC.remove(key);
        }

        public virtual void clear()
        {
            MDC.clear();
        }
    }
}