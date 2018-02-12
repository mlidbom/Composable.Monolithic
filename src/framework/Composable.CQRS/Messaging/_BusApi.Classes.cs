﻿using System;
using Composable.DDD;
using Composable.Messaging.Buses;
using Composable.Refactoring.Naming;
using Newtonsoft.Json;

// ReSharper disable RedundantNameQualifier
// ReSharper disable UnusedTypeParameter
// ReSharper disable MemberHidesStaticFromOuterClass

namespace Composable.Messaging
{
    public static partial class BusApi
    {
        public partial class StrictlyLocal
        {
            public static class Queries
            {
                public abstract class Query<TResult> : StrictlyLocal.IQuery<TResult> {}

                public class EntityLink<TResult> : StrictlyLocal.Queries.Query<TResult> where TResult : IHasPersistentIdentity<Guid>
                {
                    [JsonConstructor] public EntityLink(Guid entityId) => EntityId = entityId;
                    public Guid EntityId { get; private set; }
                }
            }

            public static class Commands
            {
                public abstract class Command : StrictlyLocal.ICommand {}

                public abstract class Command<TResult> : StrictlyLocal.ICommand<TResult> {}
            }
        }

        public static partial class Remotable
        {
            public static partial class AtMostOnce
            {
                //Todo: How can we prevent UI's from just defaulting to using a constructor that creates a new guid?
                public class Command : Remotable.AtMostOnce.ICommand
                {
                    ///<summary>How and when a <see cref="DeduplicationId"/> is generated is vital to correctly maintain the At most once delivery guarantee. When creating the command in the backend service we must generate a new <see cref="DeduplicationId"/>. When binding the command in a UI we must reuse the <see cref="DeduplicationId"/> generated by the backend. This enum helps make that important distinction explicit in your code.</summary>
                    protected enum DeduplicationIdHandling
                    {
                        ///<summary>When creating the command within the owning handler endpoint a new <see cref="Command.DeduplicationId"/> must be generated. </summary>
                        Create,
                        ///<summary>When binding the command in a UI or when deserializing it is very important to NOT create a new <see cref="Command.DeduplicationId"/> as this would break the At most once delivery guarantee.</summary>
                        Reuse
                    }

                    ///<summary>How and when a <see cref="DeduplicationId"/> is generated is vital to correctly maintain the At most once delivery guarantee. When creating the command in the backend service we must generate a new <see cref="DeduplicationId"/>. When binding the command in a UI we must reuse the <see cref="DeduplicationId"/> generated by the backend.</summary>
                    protected Command(DeduplicationIdHandling scenario) => _messageId = scenario == DeduplicationIdHandling.Create ? Guid.NewGuid() : Guid.Empty;

                    Guid _messageId;

                    public Guid DeduplicationId
                    {
                        get => _messageId;
                        set
                        {
                            if(_messageId != Guid.Empty)
                            {
                                throw new Exception($"Once {nameof(DeduplicationId)} has been set it cannot be changed. It is only settable at all because many UI technologies require it to be.");
                            }

                            _messageId = value;
                        }
                    }
                }

                public class Command<TResult> : Command, Remotable.AtMostOnce.ICommand<TResult>
                {
                    ///<summary>It is important not to set a default value if we are binding values in a UI. That would make it very easy to accidentally break the At most once guarantee. That is why you must pass the enum value here so that we can know what is happening.</summary>
                    protected Command(DeduplicationIdHandling scenario) : base(scenario) {}
                }
            }

            public static partial class NonTransactional
            {
                public static class Queries
                {
                    public abstract class Query<TResult> : Remotable.NonTransactional.IQuery<TResult> {}

                    public class EntityLink<TResult> : Remotable.NonTransactional.Queries.Query<TResult> where TResult : IHasPersistentIdentity<Guid>
                    {
                        public EntityLink() {}
                        public EntityLink(Guid entityId) => EntityId = entityId;
                        public EntityLink<TResult> WithId(Guid id) => new EntityLink<TResult>(id);
                        public Guid EntityId { get; private set; }
                    }

                    ///<summary>Implement <see cref="ICreateMyOwnResultQuery{TResult}"/> by passing a func to this base class.</summary>
                    public abstract class FuncResultQuery<TResult> : Query<TResult>, ICreateMyOwnResultQuery<TResult>
                    {
                        readonly Func<TResult> _factory;
                        protected FuncResultQuery(Func<TResult> factory) => _factory = factory;
                        public TResult CreateResult() => _factory();
                    }

                    /// <summary>Implements <see cref="ICreateMyOwnResultQuery{TResult}"/> by calling the default constructor on <typeparamref name="TResult"/></summary>
                    public class NewableResultLink<TResult> : Query<TResult>, ICreateMyOwnResultQuery<TResult>
                    {
                        static readonly Func<TResult> Constructor = System.Reflection.Constructor.For<TResult>.DefaultConstructor.Instance;
                        public TResult CreateResult() => Constructor();
                    }
                }
            }

            public static partial class ExactlyOnce
            {
                public class Command : ValueObject<Command>, Remotable.ExactlyOnce.ICommand
                {
                    public Guid DeduplicationId { get; private set; }

                    protected Command()
                        : this(Guid.NewGuid()) {}

                    Command(Guid id) => DeduplicationId = id;
                }
            }
        }

        internal static void MapTypes(ITypeMappingRegistar typeMapper)
        {
            typeMapper
               .MapTypeAndStandardCollectionTypes<BusApi.Remotable.IEvent>("1E0DB1B4-71A6-4D2E-901F-E238ABA30B63")
               .MapTypeAndStandardCollectionTypes<BusApi.Internal.EndpointInformationQuery>("D94259E4-7479-442C-99AE-D49C12CF8713")
               .MapTypeAndStandardCollectionTypes<BusApi.Internal.EndpointInformation>("2B598C6D-4893-4CB9-B4CE-7B705AD92DF9");
        }
    }
}
