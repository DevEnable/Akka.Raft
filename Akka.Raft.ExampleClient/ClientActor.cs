using System;
using Akka.Actor;

namespace Akka.Raft.ExampleClient
{
    public class ClientActor : UntypedActor
    {
        private readonly IActorRef _lead;

        public ClientActor(IActorRef leadActor)
        {
            if (leadActor == null)
            {
                throw new ArgumentNullException(nameof(leadActor));
            }

            _lead = leadActor;
        }

        protected override void OnReceive(object message)
        {
            _lead.Tell(message);
        }
    }
}
