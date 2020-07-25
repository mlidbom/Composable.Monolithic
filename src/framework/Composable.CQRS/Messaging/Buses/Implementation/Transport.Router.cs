﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Composable.Contracts;
using Composable.Refactoring.Naming;
using Composable.Serialization;
using Composable.SystemCE.LinqCE;
using Composable.SystemCE.ThreadingCE;
using NetMQ;

namespace Composable.Messaging.Buses.Implementation
{
    partial class Transport
    {
        class Router
        {
            readonly object _lock = new object();
            readonly ITypeMapper _typeMapper;

            IReadOnlyDictionary<Type, IInboxConnection> _commandHandlerRoutes = new Dictionary<Type, IInboxConnection>();
            IReadOnlyDictionary<Type, IInboxConnection> _queryHandlerRoutes = new Dictionary<Type, IInboxConnection>();
            IReadOnlyList<(Type EventType, IInboxConnection Connection)> _eventSubscriberRoutes = new List<(Type EventType, IInboxConnection Connection)>();
            IReadOnlyDictionary<Type, IReadOnlyList<IInboxConnection>> _eventSubscriberRouteCache = new Dictionary<Type, IReadOnlyList<IInboxConnection>>();

            public Router(ITypeMapper typeMapper) => _typeMapper = typeMapper;

            internal void RegisterRoutes(IInboxConnection inboxConnection, ISet<TypeId> handledTypeIds)
            {
                var eventSubscribers = new List<(Type EventType, IInboxConnection Connection)>();
                var commandHandlerRoutes = new Dictionary<Type, IInboxConnection>();
                var queryHandlerRoutes = new Dictionary<Type, IInboxConnection>();
                foreach(var typeId in handledTypeIds)
                {
                    if(_typeMapper.TryGetType(typeId, out var messageType))
                    {
                        if(IsRemoteEvent(messageType))
                        {
                            eventSubscribers.Add((messageType, inboxConnection));
                        } else if(IsRemoteCommand(messageType))
                        {
                            commandHandlerRoutes.Add(messageType, inboxConnection);
                        } else if(IsRemoteQuery(messageType))
                        {
                            queryHandlerRoutes.Add(messageType, inboxConnection);
                        } else
                        {
                            throw new Exception($"Type {typeId} is neither a remote command, event or query.");
                        }
                    }
                }

                lock(_lock)
                {
                    if(eventSubscribers.Count > 0)
                    {
                        ThreadSafe.AddRangeToCopyAndReplace(ref _eventSubscriberRoutes, eventSubscribers);
                        _eventSubscriberRouteCache = new Dictionary<Type, IReadOnlyList<IInboxConnection>>();
                    }

                    if(commandHandlerRoutes.Count > 0)
                        ThreadSafe.AddRangeToCopyAndReplace(ref _commandHandlerRoutes, commandHandlerRoutes);

                    if(queryHandlerRoutes.Count > 0)
                        ThreadSafe.AddRangeToCopyAndReplace(ref _queryHandlerRoutes, queryHandlerRoutes);
                }
            }

            internal IInboxConnection ConnectionToHandlerFor(MessageTypes.Remotable.ICommand command) =>
                _commandHandlerRoutes.TryGetValue(command.GetType(), out var connection)
                    ? connection
                    : throw new NoHandlerForMessageTypeException(command.GetType());

            internal IInboxConnection ConnectionToHandlerFor(MessageTypes.Remotable.NonTransactional.IQuery query) =>
                _queryHandlerRoutes.TryGetValue(query.GetType(), out var connection)
                    ? connection
                    : throw new NoHandlerForMessageTypeException(query.GetType());

            internal IReadOnlyList<IInboxConnection> SubscriberConnectionsFor(MessageTypes.Remotable.ExactlyOnce.IEvent @event)
            {
                if(_eventSubscriberRouteCache.TryGetValue(@event.GetType(), out var connection)) return connection;

                var subscriberConnections = _eventSubscriberRoutes
                                           .Where(route => route.EventType.IsInstanceOfType(@event))
                                           .Select(route => route.Connection)
                                           .ToArray();


                ThreadSafe.AddToCopyAndReplace(ref _eventSubscriberRouteCache, @event.GetType(), subscriberConnections);
                return subscriberConnections;
            }

            static bool IsRemoteCommand(Type type) => typeof(MessageTypes.Remotable.ICommand).IsAssignableFrom(type);
            static bool IsRemoteEvent(Type type) => typeof(MessageTypes.Remotable.ExactlyOnce.IEvent).IsAssignableFrom(type);
            static bool IsRemoteQuery(Type type) => typeof(MessageTypes.Remotable.NonTransactional.IQuery).IsAssignableFrom(type);
        }
    }
}