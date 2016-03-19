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

    public class CannotApplyValueMessage<T>
    {
        public T Value { get; private set; }

        public string Reason { get; private set; }


        public CannotApplyValueMessage(T value, string reason)
        {
            Value = value;
            Reason = reason;
        } 
    }

}
