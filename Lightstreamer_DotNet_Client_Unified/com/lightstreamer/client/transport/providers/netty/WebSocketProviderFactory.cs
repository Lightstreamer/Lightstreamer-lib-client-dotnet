namespace com.lightstreamer.client.transport.providers.netty
{
    using SessionThread = com.lightstreamer.client.session.SessionThread;

    public class WebSocketProviderFactory : TransportFactory<WebSocketProvider>
    {
        public override WebSocketProvider getInstance(SessionThread thread)
        {
            return new NettyWebSocketProvider();
        }

        public override bool ResponseBuffered
        {
            get
            {
                return false;
            }
        }
    }
}