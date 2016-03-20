using System;
using Akka.Actor;

namespace Akka.Raft
{
    public class IdentifyLeaderMessage
    {
    }

    public class LeaderMessage
    {
        public IActorRef Leader { get; private set; }

        public int Term { get; private set; }

        public LeaderMessage(int term, IActorRef leader)
        {
            if (leader == null)
            {
                throw new ArgumentNullException(nameof(leader));
            }

            Term = term;
            Leader = leader;
        }

        public override bool Equals(object obj)
        {
            LeaderMessage other = obj as LeaderMessage;

            if (other == null)
            {
                return false;
            }

            return Term == other.Term && Leader == other.Leader;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Leader.GetHashCode() *397 ^ Term;
            }
        }
    }

}
