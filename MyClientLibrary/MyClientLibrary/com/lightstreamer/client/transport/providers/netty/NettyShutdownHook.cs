namespace com.lightstreamer.client.transport.providers.netty
{
    using ThreadShutdownHook = com.lightstreamer.util.threads.ThreadShutdownHook;

    /// <summary>
    /// Releases the resources acquired by Netty library. It is used by <seealso cref="LightstreamerClient#disconnectFuture()"/>.
    /// </summary>
    public class NettyShutdownHook : ThreadShutdownHook
    {

        public virtual void onShutdown()
        {
            SingletonFactory.instance.closeAsync();
        }
    }
}