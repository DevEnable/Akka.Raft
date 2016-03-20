using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Routing;

namespace Akka.Raft
{
    public class RaftClientActor<T> : ReceiveActor, IWithUnboundedStash
    {
        private IActorRef _nodes;
        private IActorRef _leader = ActorRefs.Nobody;

        private int _nodeCount;
        private NodeLeaderRegistrations _registrations;

        public IStash Stash { get; set; }

        protected override void PreStart()
        {
            base.PreStart();

            _nodes = Context.ActorOf(Props.Empty.WithRouter(FromConfig.Instance), "nodes");

            Become(NoLeader);
        }

        private void NoLeader()
        {
            Receive<Routees>(m =>
            {
                _nodeCount = m.Members.Count();
                
                Become(QueryingLeader);
            });

            Receive<T>(m => Stash.Stash());

            _nodes.Ask<Routees>(new GetRoutees(), TimeSpan.FromSeconds(1)).PipeTo(Self);
        }

        private void QueryingLeader()
        {
            Receive<LeaderMessage>(m =>
            {
                // TODO - Some sort of timeout if a leader hasn't been identified.
                IdentifiedLeader identifiedLeader = _registrations.Add(Sender, m);

                if (identifiedLeader.LeaderFound)
                {
                    _leader = identifiedLeader.Leader;
                    Become(LeaderIdentified);
                }
            });

            Receive<T>(m => Stash.Stash());

            _registrations = new NodeLeaderRegistrations(_nodeCount);
            _nodes.Tell(new IdentifyLeaderMessage());
        }

        private void LeaderIdentified()
        {
            Receive<T>(m =>
            {
                _leader.Tell(m);
            });

            Stash.UnstashAll();
        }

        private class NodeLeaderRegistrations
        {
            private readonly int _majority;
            private readonly Dictionary<IActorRef, LeaderMessage> _nodeLeaders = new Dictionary<IActorRef, LeaderMessage>();

            public NodeLeaderRegistrations(int nodeCount)
            {
                _majority = (int)Math.Ceiling((double)nodeCount / 2);
            }

            public IdentifiedLeader Add(IActorRef sender, LeaderMessage registration)
            {
                _nodeLeaders[sender] = registration;

                // TODO - Consider registrations that are not in the latest term, or that come back as Nobody
                if (_nodeLeaders.Count >= _majority)
                {
                    IActorRef leader = (from r in _nodeLeaders
                                    group r by r.Value.Leader into leaderVotes
                                    where leaderVotes.Count() >= _majority
                                    select leaderVotes.Key).FirstOrDefault();

                    if (leader != null)
                    {
                        return new IdentifiedLeader(true, leader);
                    }
                }

                return IdentifiedLeader.None;
            }
        }

        private class IdentifiedLeader
        {
            public bool LeaderFound { get; private set; }

            public IActorRef Leader { get; private set; }

            public static IdentifiedLeader None
            {
                get { return new IdentifiedLeader(false, ActorRefs.Nobody); }
            }

            public IdentifiedLeader(bool leaderFound, IActorRef leader)
            {
                LeaderFound = leaderFound;
                Leader = leader;
            }
        }

    }
}
