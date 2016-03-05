using System;
using Akka.Actor;
using Akka.Event;
using Akka.Routing;

namespace Akka.Raft
{
    public class RaftActor<T> : ReceiveActor
    {
        private T _value;
        private IActorRef _leader = ActorRefs.Nobody;
        private IActorRef _colleagues;
        private ICancelable _schedulingCancellation;

        // ReSharper disable once StaticMemberInGenericType
        private static readonly TimeSpan LeaderRecieveTimeout = TimeSpan.FromMilliseconds(150);

        protected override void PreStart()
        {
            base.PreStart();

            // TODO - Figure out the router to use.
            _colleagues = Context.ActorOf(Props.Empty.WithRouter(RouterConfig.NoRouter));

            Become(Follower);
        }

        private void Follower()
        {
            Receive<BecomeCandidateMessage>(m => Become(Candidate));

            CommonHandlers();

            if (_leader.IsNobody())
            {
                _schedulingCancellation = Context.System.Scheduler.ScheduleTellOnceCancelable(LeaderRecieveTimeout, Self, new BecomeCandidateMessage(), Self);
            }
        }

        private void Candidate()
        {
            Receive<RequestVoteMessage>(m => { });

            CommonHandlers();
        }

        private void Leader()
        {
            CommonHandlers();
        }
        
        private void CommonHandlers()
        {
            Receive<T>(m => _value = m);
            ReceiveAny(m => Context.GetLogger().Warning("Unexpected message of type {0}", 
                m != null ? m.GetType().FullName : "NULL"));
        }

        private class BecomeCandidateMessage
        { }
    }
}
