namespace com.lightstreamer.util
{
    /// <summary>
    /// Visitor of collections.
    /// </summary>
    public interface Visitor<T>
    {
        /// <summary>
        /// Executes the operation for each element of a collection.
        /// </summary>
        void visit(T listener);
    }
}