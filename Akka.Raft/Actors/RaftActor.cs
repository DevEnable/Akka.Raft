using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Routing;

namespace Akka.Raft
{
    public class RaftActor<T> : ReceiveActor
    {
        private Term _term;
        private LinkedList<T> _values;
        private IActorRef _leader = ActorRefs.Nobody;
        private IActorRef _colleagues;
        private ICancelable _schedulingCancellation;

        // ReSharper disable once StaticMemberInGenericType
        private static readonly TimeSpan LeaderRecieveTimeout = TimeSpan.FromMilliseconds(150);

        private bool HasLeader => !_leader.IsNobody();

        protected override void PreStart()
        {
            base.PreStart();

            _term = Term.NotSet;
            // TODO - Figure out the router to use.
            _colleagues = Context.ActorOf(Props.Empty.WithRouter(RouterConfig.NoRouter));

            Become(Follower);
        }

        private void Follower()
        {
            Receive<BecomeCandidateMessage>(m => Become(Candidate));
            Receive<RequestVoteMessage>(m =>
            {
                if (_term.TermNumber < m.Term)
                {
                    // New term
                    _term = new Term(m.Term, Sender);
                    CancelScheduling();
                    Sender.Tell(new VoteMessage(m.Term, Sender));
                }
                else if (_term.TermNumber == m.Term)
                {
                    Sender.Tell(new VoteMessage(m.Term, _term.VotedFor));
                }
                else
                {
                    // TODO - Figure out what to do for this edge case.
                }
                
            });

            Receive<T>(m =>
            {
                if (Sender.Equals(_leader))
                {
                    _values.AddFirst(m);
                }
                else
                {
                    Context.GetLogger()
                        .Warning("Received value from a node that is not a leader, discarding value {0}", m);
                }
            });


            CommonHandlers();

            if (!HasLeader)
            {
                _schedulingCancellation = Context.System.Scheduler.ScheduleTellOnceCancelable(LeaderRecieveTimeout, Self, new BecomeCandidateMessage(), Self);
            }
        }

        private void Candidate()
        {
            Receive<RequestVoteMessage>(m => { });

            Receive<VoteMessage>(m =>
            {
                if (m.Term == _term.TermNumber)
                {
                    if (m.VotedFor.Equals(Self))
                    {
                        _term.ReceivedVotes.Add(m.VotedFor);
                    }
                    else
                    {
                        _term.MissedVotes.Add(m.VotedFor);
                    }
                }
            });

            // TODO - Find out logic for triggering the results of an election and promoting a leader.

            CommonHandlers();

            _term = _term.NextTerm(Self);
            _colleagues.Tell(new RequestVoteMessage(_term.TermNumber));
        }

        private void Leader()
        {
            Receive<T>(m =>
            {
                // Message sent to itself through the router, discard it.
                if (Sender.Equals(Self))
                {
                    return;
                }

                _values.AddFirst(m);
                _colleagues.Tell(m);
            });
            CommonHandlers();
        }
        
        private void CommonHandlers()
        {
            Receive<GetValueMessage>(m =>
            {
                object currentValue = _values.Any() ? (object)_values.First : new NoValueMessage();
                Sender.Tell(currentValue);
            });

            ReceiveAny(m => Context.GetLogger().Warning("Unexpected message of type {0}", 
                m != null ? m.GetType().FullName : "NULL"));
        }

        private void CancelScheduling()
        {
            if (_schedulingCancellation != null)
            {
                _schedulingCancellation.Cancel();
                _schedulingCancellation = null;
            }
        }

        private class BecomeCandidateMessage
        { }
    }
}
