using DotNetty.Transport.Channels;
using System;

namespace DotNetty.Handlers.Proxy
{
    public class ProxyConnectException : ConnectException
    {
        private const long serialVersionUID = 5211364632246265538L;

        public ProxyConnectException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}