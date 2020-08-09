﻿using System;
using Composable.DDD;
using Composable.Messaging;

namespace Composable.Persistence.EventStore
{
    public class AggregateEvent<TWrapperEventClass, TBaseEventInterface> : MessageTypes.WrapperEvent<TWrapperEventClass, TBaseEventInterface>

        where TBaseEventInterface : IAggregateEvent, MessageTypes.IWrapperEvent<TBaseEventInterface>
        where TWrapperEventClass : MessageTypes.IWrapperEvent<TWrapperEventClass, TBaseEventInterface>
    {
        public AggregateEvent(TBaseEventInterface @event) : base(@event) {}
    }
 
    public abstract class AggregateEvent : ValueObject<AggregateEvent>, IAggregateEvent
    {
        protected AggregateEvent()
        {
            EventId = Guid.NewGuid();
            UtcTimeStamp = DateTime.UtcNow;//Todo:bug: Should use timesource.
        }

        protected AggregateEvent(Guid aggregateId) : this() => AggregateId = aggregateId;

        /*Refactor: Consider making these fields read-only and then generating accessor for them at runtime. This would make the special nature of changing them more explicit.
         And it would remove the requirement that this class is used. Another class could be used and we would detect that and generate new setters for that class, requiring 
        only that it had private setters (including the one generated for a readonly property by the runtime.).
        */
        public Guid EventId { get; internal set; }
        public int AggregateVersion { get; internal set; }

        public Guid AggregateId { get; internal set; }
        public DateTime UtcTimeStamp { get; internal set; }
    }
}
