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

        // TODO - Add the equivilant of F#'s Some / None
        private T _uncommitedValue;

        // TODO - Consider Akka.Persistence for the values
        private readonly LinkedList<T> _values = new LinkedList<T>();
        private IActorRef _leader = ActorRefs.Nobody;
        private IActorRef _colleagues;
        private ICancelable _schedulingCancellation;
        
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
                    ScheduleElection();
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

            Receive<T>(m => !HasLeader, m =>
            {
                Context.GetLogger()
                        .Warning(NoLeaderReason, m);
                Sender.Tell(new CannotApplyValueMessage<T>(m, "Cannot apply this value as there is no leader."));
            });

            Receive<T>(m =>
            {
                if (Sender.Equals(_leader))
                {
                    _uncommitedValue = m;
                    _leader.Tell(new ValueReceivedMessage<T>(m));
                }
                else
                {
                    Context.GetLogger()
                        .Warning("Received value from a node that is not a leader, discarding value {0}", m);
                    Sender.Tell(new CannotApplyValueMessage<T>(m, "Cannot apply this value as the sender is not the leader."));
                }
            });

            Receive<CommitValueMessage>(m =>
            {
                _values.AddFirst(_uncommitedValue);
                _uncommitedValue = default(T);
            });

            CommonHandlers();

            if (!HasLeader)
            {
                ScheduleElection();
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

            Receive<T>(m => NoLeaderHandler(m));

            Receive<ValueReceivedMessage<T>>(m => NoLeaderHandler(m));

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

                _uncommitedValue = m;
                _colleagues.Tell(m);
            });

            Receive<ValueReceivedMessage<T>>(m =>
            {
                // TODO - Figure the best way to manage the number of consensus nodes.  GetRoutees, pub / sub to leader?

                // TODO - Once majority (> 50%) of nodes have written the value, commit it on the lead.

                _values.AddFirst(_uncommitedValue);
                _uncommitedValue = default(T);

                _colleagues.Tell(new CommitValueMessage());
            });

            Receive<CommitValueMessage>(m => { });

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

        private void NoLeaderHandler<TMessage>(TMessage message)
        {
            Context.GetLogger()
                        .Warning("Received value from a node when there is no leader, discarding value {0}", message);
            Sender.Tell(new CannotApplyValueMessage<TMessage>(message, "Cannot handle this message as a leader has not been elected."));
        }

        private void CancelScheduling()
        {
            if (_schedulingCancellation != null)
            {
                _schedulingCancellation.Cancel();
                _schedulingCancellation = null;
            }
        }

        private void ScheduleElection()
        {
            _schedulingCancellation = Context.System.Scheduler.ScheduleTellOnceCancelable(GetElectionTimeout(), Self, new BecomeCandidateMessage(), Self);
        }

        private static TimeSpan GetElectionTimeout()
        {
            // TODO - Fix up the threading and best practice - https://msdn.microsoft.com/en-us/library/system.random.aspx

            Random rand = new Random();

            return TimeSpan.FromMilliseconds(150 + rand.Next(0, 151));
        }

        private class BecomeCandidateMessage
        { }
    }
}
