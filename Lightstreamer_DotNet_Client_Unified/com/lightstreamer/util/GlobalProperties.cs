namespace com.lightstreamer.util
{
    /// <summary>
    /// Singleton class storing global properties affecting the behavior of the library.
    /// </summary>
    public class GlobalProperties
    {
        public static readonly GlobalProperties INSTANCE = new GlobalProperties();

        private System.Net.Security.RemoteCertificateValidationCallback trustManagerFactory;

        private GlobalProperties()
        {
        }

        public virtual System.Net.Security.RemoteCertificateValidationCallback TrustManagerFactory
        {
            get
            {
                lock (this)
                {
                    return trustManagerFactory;
                }
            }
            set
            {
                lock (this)
                {
                    this.trustManagerFactory = value;
                }
            }
        }
    }
}