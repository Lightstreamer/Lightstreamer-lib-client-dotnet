using DotNetty.Buffers;
using DotNetty.Codecs.Base64;
using DotNetty.Codecs.Http;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

using System;
using System.Net;
using System.Text;

namespace DotNetty.Handlers.Proxy
{
    public sealed class HttpProxyHandler : ProxyHandler
    {
        private const string PROTOCOL = "http";
        private const string AUTH_BASIC = "basic";

        private readonly HttpClientCodec codec = new HttpClientCodec();
        private readonly ICharSequence authorization;
        private readonly HttpHeaders outboundHeaders;
        private readonly bool ignoreDefaultPortsInConnectHostHeader;
        private HttpResponseStatus status;
        private HttpHeaders inboundHeaders;

        public string Username { get; }

        public string Password { get; }

        public HttpProxyHandler(EndPoint proxyAddress) : this(proxyAddress, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers) : this(proxyAddress, headers, false)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers, bool ignoreDefaultPortsInConnectHostHeader) : base(proxyAddress)
        {
            this.Username = null;
            this.Password = null;
            authorization = null;
            this.outboundHeaders = headers;
            this.ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password) : this(proxyAddress, username, password, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password, HttpHeaders headers) : this(proxyAddress, username, password, headers, false)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password, HttpHeaders headers, bool ignoreDefaultPortsInConnectHostHeader) : base(proxyAddress)
        {
            if (string.ReferenceEquals(username, null))
            {
                throw new System.NullReferenceException("username");
            }
            if (string.ReferenceEquals(password, null))
            {
                throw new System.NullReferenceException("password");
            }
            this.Username = username;
            this.Password = password;

            IByteBuffer authz = Unpooled.Buffer();
            authz.WriteString(username + ':' + password, Encoding.UTF8);

            IByteBuffer authzBase64 = Base64.Encode(authz, Base64Dialect.URL_SAFE);

            authorization = new AsciiString("Basic " + authzBase64.ToString(Encoding.ASCII));

            authz.SafeRelease();
            authzBase64.SafeRelease();

            this.outboundHeaders = headers;
            this.ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public override string protocol()
        {
            return PROTOCOL;
        }

        public override string authScheme()
        {
            return authorization != null ? AUTH_BASIC : AUTH_NONE;
        }

        protected internal override void addCodec(IChannelHandlerContext ctx)
        {
            IChannelPipeline p = ctx.Channel.Pipeline;
            string name = ctx.Name;
            p.AddBefore(name, null, codec);
        }

        protected internal override void removeEncoder(IChannelHandlerContext ctx)
        {
            codec.RemoveOutboundHandler();
        }

        protected internal override void removeDecoder(IChannelHandlerContext ctx)
        {
            codec.RemoveInboundHandler();
        }

        protected internal override object newInitialMessage(IChannelHandlerContext ctx)
        {
            String address = DestinationAddress.ToString();

            IFullHttpRequest req = new DefaultFullHttpRequest(Codecs.Http.HttpVersion.Http11, HttpMethod.Connect, address, Unpooled.Empty, false);

            req.Headers.Set(HttpHeaderNames.Host, address);

            if (authorization != null)
            {
                req.Headers.Remove(HttpHeaderNames.ProxyAuthorization);

                req.Headers.Set(HttpHeaderNames.ProxyAuthorization, authorization);
            }

            if (outboundHeaders != null)
            {
                req.Headers.Add(outboundHeaders);
            }

            return req;
        }

        protected internal override bool handleResponse(IChannelHandlerContext ctx, object response)
        {
            if (( response is IFullHttpResponse ) || ( response is DefaultHttpResponse ))
            {
                DefaultHttpResponse def = (DefaultHttpResponse)response;
                if (def != null)
                {
                    status = def.Status;

                    inboundHeaders = def.Headers;
                }

            }

            bool finished = response is ILastHttpContent;
            if (finished)
            {
                if (status == null)
                {
                    throw new HttpProxyConnectException(exceptionMessage("missing response"), inboundHeaders);
                }
                if (status.Code != 200)
                {
                    throw new HttpProxyConnectException(exceptionMessage("status: " + status), inboundHeaders);
                }
            }

            return finished;
        }

        /// <summary>
        /// Specific case of a connection failure, which may include headers from the proxy.
        /// </summary>
        public sealed class HttpProxyConnectException : ProxyConnectException
        {
            internal const long serialVersionUID = -8824334609292146066L;

            private readonly HttpHeaders headers;

            /// <param name="message"> The failure message. </param>
            /// <param name="headers"> Header associated with the connection failure.  May be {@code null}. </param>
            public HttpProxyConnectException(string message, HttpHeaders headers) : base(message, new Exception(message))
            {
                this.headers = headers;
            }

            internal HttpHeaders Headers => headers;
        }
    }
}