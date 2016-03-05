using Akka.Actor;

namespace Akka.Raft
{
    public class RequestVoteMessage
    {
        public int Term { get; private set; }

        public RequestVoteMessage(int term)
        {
            Term = term;
        }
    }

    public class VoteMessage
    {
        public int Term { get; private set; }

        public IActorRef VotedFor { get; private set; }

        public VoteMessage(int term, IActorRef votedFor)
        {
            Term = term;
            VotedFor = votedFor;
        }
    }

}
