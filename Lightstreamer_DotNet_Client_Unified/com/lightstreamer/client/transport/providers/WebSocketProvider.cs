using System.Collections.Generic;

namespace com.lightstreamer.client.transport.providers
{
	using ThreadShutdownHook = com.lightstreamer.util.threads.ThreadShutdownHook;

	/// <summary>
	/// Interface used to decouple the application classes from a specific WebSocket implementation (for example Netty <seealso cref="WebSocketClient"/>).
	/// Instances of this type are obtained through the factory <seealso cref="TransportFactory#getDefaultWebSocketFactory()"/>.
	/// </summary>
	public interface WebSocketProvider
	{

		/// <summary>
		/// Opens a WebSocket connection. </summary>
		/// <param name="address"> host address </param>
		/// <param name="networkListener"> listens to connection events (opening, closing, message receiving, error) </param>
		/// <param name="extraHeaders"> headers to be added during WebSocket handshake </param>
		/// <param name="cookies"> cookies to be added during WebSocket handshake </param>
		/// <param name="proxy"> if not null, the client connects to the proxy and the proxy forwards the messages to the host  </param>
		void connect(string address, SessionRequestListener networkListener, IDictionary<string, string> extraHeaders, string cookies, Proxy proxy, long timeout);

		/// <summary>
		/// Sends a message.
		/// <para>
		/// <b>NB</b> When the message has been successfully written on WebSocket,
		/// it is mandatory to notify the method <seealso cref="RequestListener#onOpen()"/>.
		/// </para>
		/// </summary>
		void send(string message, RequestListener listener);

		/// <summary>
		/// Closes the connection.
		/// </summary>
		void disconnect();

		/// <summary>
		/// Returns a callback to free the resources (threads, sockets...) allocated by the provider.
		/// </summary>
		ThreadShutdownHook ThreadShutdownHook {get;}
	}
}