namespace com.lightstreamer.client.session
{
    /// <summary>
    /// A server session.
    /// <para>
    /// <b>NB</b>
    /// The class <seealso cref="Session"/>, notwithstanding the name, does not represent a server session because in general it has a 
    /// shorter life span than the corresponding server session. Rather it represents the current stream connection 
    /// (a server session is made of a sequence of stream connections).
    /// </para>
    /// </summary>
    public class ServerSession
    {
        private State state;
        private Session streamConnection;

        /// <summary>
        /// Builds a server session using the specified stream connection.
        /// </summary>
        public ServerSession(Session initialStreamConnection)
        {
            this.state = State.OPEN;
            this.streamConnection = initialStreamConnection;
        }

        /// <summary>
        /// Changes the current stream connection.
        /// </summary>
        public virtual Session NewStreamConnection
        {
            set
            {
                this.streamConnection = value;
            }
        }

        /// <summary>
        /// Returns whether the current stream connection is the same as the specified connection.
        /// </summary>
        public virtual bool isSameStreamConnection(Session tutorStreamConnection)
        {
            return streamConnection == tutorStreamConnection;
        }

        /// <summary>
        /// Returns whether the underlying stream connection is using a HTTP transport.
        /// </summary>
        public virtual bool TransportHttp
        {
            get
            {
                return streamConnection is SessionHTTP;
            }
        }

        /// <summary>
        /// Returns whether the underlying stream connection is using a WebSocket transport.
        /// </summary>
        public virtual bool TransportWS
        {
            get
            {
                return streamConnection is SessionWS;
            }
        }

        public virtual bool Open
        {
            get
            {
                return state.Equals(State.OPEN);
            }
        }

        public virtual bool Closed
        {
            get
            {
                return state.Equals(State.CLOSED);
            }
        }

        public virtual void close()
        {
            state = State.CLOSED;
        }

        private enum State
        {
            OPEN,
            CLOSED
        }
    }

}