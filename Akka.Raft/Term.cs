using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Raft
{
    public class Term
    {
        private readonly List<IActorRef> _receivedVotes = new List<IActorRef>();
        private readonly IList<IActorRef> _missedVotes = new List<IActorRef>(); 

        public int TermNumber { get; private set; }

        public IActorRef VotedFor { get; private set; }

        public IList<IActorRef> ReceivedVotes => _receivedVotes;
        public IList<IActorRef> MissedVotes => _missedVotes; 

        public bool HasCastVote => !VotedFor.Equals(ActorRefs.Nobody);

        public static Term NotSet => new Term(0);

        public Term(int termNumber)
        {
            TermNumber = termNumber;
            VotedFor = ActorRefs.Nobody;
        }

        public Term(int termNumber, IActorRef votedFor)
        {
            TermNumber = termNumber;
            VotedFor = votedFor;
        }

        public Term NextTerm(IActorRef selfVote)
        {
            Term term = new Term(TermNumber + 1, selfVote);
            term.ReceivedVotes.Add(selfVote);

            return term;
        }
    }
}
