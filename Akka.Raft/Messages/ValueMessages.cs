namespace Akka.Raft
{
    public class GetValueMessage
    {
    }

    public class NoValueMessage
    {
    }

    public class CommitValueMessage
    {
        
    }

    public class ValueReceivedMessage<T>
    {
        public T Value { get; private set; }

        public ValueReceivedMessage(T value)
        {
            Value = value;
        }
    }

}
