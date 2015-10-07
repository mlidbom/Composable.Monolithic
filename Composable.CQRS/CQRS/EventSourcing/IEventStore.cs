using System;
using System.Collections.Generic;

namespace Composable.CQRS.EventSourcing
{
    public interface IEventStore : IDisposable
    {
        IEnumerable<IAggregateRootEvent> GetAggregateHistory(Guid id);
        void SaveEvents(IEnumerable<IAggregateRootEvent> events);
        IEnumerable<IAggregateRootEvent> StreamEventsAfterEventWithId(Guid? startAfterEventId);
        void DeleteEvents(Guid aggregateId);
        IEnumerable<Guid> StreamAggregateIdsInCreationOrder(Type eventBaseType = null);
    }
}