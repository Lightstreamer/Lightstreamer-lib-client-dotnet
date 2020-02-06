namespace com.lightstreamer.client.requests
{
    using IdGenerator = com.lightstreamer.util.IdGenerator;

    public abstract class NumberedRequest : LightstreamerRequest
    {
        protected internal readonly long requestId = IdGenerator.NextRequestId;

        public NumberedRequest()
        {
            addParameter("LS_reqId", requestId);
        }

        public long RequestId
        {
            get
            {
                return requestId;
            }
        }
    }
}