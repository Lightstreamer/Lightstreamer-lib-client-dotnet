#region License
/*
 * Copyright (c) Lightstreamer Srl
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion License

namespace com.lightstreamer.client.transport.providers.netty.pool
{
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    public class WebSocketClientHandler : SimpleChannelInboundHandler<object>
    {
        readonly WebSocketClientHandshaker handshaker;
        readonly TaskCompletionSource completionSource;

        public WebSocketClientHandler(WebSocketClientHandshaker handshaker)
        {
            this.handshaker = handshaker;
            this.completionSource = new TaskCompletionSource();
        }

        public Task HandshakeCompletion => this.completionSource.Task;

        public override void ChannelActive(IChannelHandlerContext ctx) =>
            this.handshaker.HandshakeAsync(ctx.Channel).LinkOutcome(this.completionSource);

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            Console.WriteLine("WebSocket Client disconnected!");
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            IChannel ch = ctx.Channel;
            if (!this.handshaker.IsHandshakeComplete)
            {
                try
                {
                    this.handshaker.FinishHandshake(ch, (IFullHttpResponse)msg);
                    this.completionSource.TryComplete();
                }
                catch (WebSocketHandshakeException e)
                {
                    this.completionSource.TrySetException(e);
                }

                return;
            }


            if (msg is IFullHttpResponse response)
            {
                throw new InvalidOperationException(
                    $"Unexpected FullHttpResponse (getStatus={response.Status}, content={response.Content.ToString(Encoding.UTF8)})");
            }

            if (msg is TextWebSocketFrame textFrame)
            {
                Console.WriteLine($"WebSocket Client received message: {textFrame.Text()}");
            }
            else if (msg is PongWebSocketFrame)
            {
                Console.WriteLine("WebSocket Client received pong");
            }
            else if (msg is CloseWebSocketFrame)
            {
                Console.WriteLine("WebSocket Client received closing");
                ch.CloseAsync();
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            this.completionSource.TrySetException(exception);
            ctx.CloseAsync();
        }
    }
}