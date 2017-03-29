﻿using System;
using Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated.Domain;
using Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated.Domain.Events;
using Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated.Domain.Events.Implementation;
using Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated.Domain.Events.PropertyUpdated;
using Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated.Domain.UI.QueryModels;
using Composable.DependencyInjection;
using Composable.GenericAbstractions.Time;
using Composable.Messaging.Events;
using Composable.Persistence.DocumentDb;
using Composable.Persistence.EventSourcing;
using Composable.Persistence.EventStore;
using Composable.Persistence.EventStore.AggregateRoots;
using Composable.Persistence.EventStore.Query.Models.Generators;
using Composable.UnitsOfWork;
using FluentAssertions;
using NUnit.Framework;

namespace Composable.CQRS.Tests.CQRS.Query.Models.AutoGenerated
{
    [TestFixture]
    public class QueryModelGeneratingDocumentDbReaderTests2
    {
        IServiceLocator _serviceLocator;

        [SetUp]
        public void CreateContainer()
        {
            _serviceLocator = DependencyInjectionContainer.CreateServiceLocatorForTesting(
                                                                                    container => container.Register(

                                                                                                                    Component.For<IEventStore>()
                                                                                                                              .ImplementedBy<InMemoryEventStore>()
                                                                                                                              .LifestyleSingleton(),
                                                                                                                    Component.For<IEventStoreSession, IEventStoreReader, IUnitOfWorkParticipant>()
                                                                                                                              .ImplementedBy<EventStoreSession>()
                                                                                                                              .LifestyleScoped(),
                                                                                                                    Component.For<IDocumentDbReader, IVersioningDocumentDbReader>()
                                                                                                                              .ImplementedBy<QueryModelGeneratingDocumentDbReader>()
                                                                                                                              .LifestyleScoped(),
                                                                                                                    Component.For<IQueryModelGenerator, IQueryModelGenerator<MyAccountQueryModel>>()
                                                                                                                              .ImplementedBy<AccountQueryModelGenerator>()
                                                                                                                              .LifestyleScoped()
                                                                                                                   ));
        }

        [Test]
        public void ThrowsExceptionIfInstanceDoesNotExist()
        {
            using(_serviceLocator.BeginScope())
            {
                var reader = _serviceLocator.Resolve<IDocumentDbReader>();
                reader.Invoking(me => me.Get<MyAccountQueryModel>(Guid.NewGuid()))
                    .ShouldThrow<Exception>();
            }
        }

        [Test]
        public void CanFetchQueryModelAfterAggregateHasBeenCreated()
        {
            using(_serviceLocator.BeginScope())
            {
                var aggregates = _serviceLocator.Resolve<IEventStoreSession>();
                var accountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                MyAccount registered;
                using(var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope())
                {
                    registered = MyAccount.Register(aggregates, accountId, "email", "password");
                    transaction.Commit();
                }


                registered.Email.Should().Be("email");
                registered.Password.Should().Be("password");


                var reader = _serviceLocator.Resolve<IDocumentDbReader>();
                var loadedModel = reader.Get<MyAccountQueryModel>(registered.Id);

                loadedModel.Should().NotBe(null);
                loadedModel.Id.Should().Be(accountId);
                loadedModel.Email.Should().Be(registered.Email);
                loadedModel.Password.Should().Be(registered.Password);
            }
        }

        [Test]
        public void ThrowsExceptionWhenTryingToFetchDeletedEntity()
        {
            using (_serviceLocator.BeginScope())
            {
                var aggregates = _serviceLocator.Resolve<IEventStoreSession>();
                var accountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                MyAccount registered;
                using (var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope())
                {
                    registered = MyAccount.Register(aggregates, accountId, "email", "password");
                    transaction.Commit();
                }

                var reader = _serviceLocator.Resolve<IDocumentDbReader>();
                reader.Get<MyAccountQueryModel>(registered.Id);//Here it exists

                using (var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope())
                {
                    registered.Delete();
                    transaction.Commit();
                }

                using(_serviceLocator.BeginScope())
                {
                    var reader2 = _serviceLocator.Resolve<IDocumentDbReader>();
                    reader2.Invoking(me => me.Get<MyAccountQueryModel>(registered.Id))
                        .ShouldThrow<Exception>();
                }
            }
        }

        [Test]
        public void ReturnsUpdatedDataAfterTransactionHasCommitted()
        {
            using(_serviceLocator.BeginScope())
            {
                var accountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                MyAccount registered;

                var aggregates = _serviceLocator.Resolve<IEventStoreSession>();
                using(var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope())
                {
                    registered = MyAccount.Register(aggregates, accountId, "email", "password");
                    transaction.Commit();
                }

                _serviceLocator.Resolve<IDocumentDbReader>()
                    .Get<MyAccountQueryModel>(registered.Id); //Make sure we read it once so caches etc get involved.

                using(var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope()) //Update it.
                {
                    registered.ChangeEmail("newEmail");
                    transaction.Commit();
                }

                using(_serviceLocator.BeginScope())
                {
                    var loadedModel = _serviceLocator.Resolve<IDocumentDbReader>()
                        .Get<MyAccountQueryModel>(registered.Id);

                    loadedModel.Should().NotBe(null);
                    loadedModel.Email.Should().Be("newEmail");
                }
            }
        }

        [Test]
        public void CanReturnPreviousVersionsOfQueryModel()
        {
            using (_serviceLocator.BeginScope())
            {
                var accountId = Guid.Parse("00000000-0000-0000-0000-000000000001");

                MyAccount registered;

                var aggregates = _serviceLocator.Resolve<IEventStoreSession>();
                using (var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope())
                {
                    registered = MyAccount.Register(aggregates, accountId, "originalEmail", "password");
                    transaction.Commit();
                }

                _serviceLocator.Resolve<IVersioningDocumentDbReader>()
                    .GetVersion<MyAccountQueryModel>(registered.Id, registered.Version); //Make sure we read it once so caches etc get involved.

                using (var transaction = _serviceLocator.BeginTransactionalUnitOfWorkScope()) //Update it.
                {
                    registered.ChangeEmail("newEmail1");
                    registered.ChangeEmail("newEmail2");
                    registered.ChangeEmail("newEmail3");
                    transaction.Commit();
                }

                using (_serviceLocator.BeginScope())
                {
                    var loadedModel = _serviceLocator.Resolve<IVersioningDocumentDbReader>()
                        .Get<MyAccountQueryModel>(registered.Id);

                    loadedModel.Should().NotBe(null);
                    loadedModel.Email.Should().Be("newEmail3");

                    _serviceLocator.Resolve<IVersioningDocumentDbReader>()
                        .GetVersion<MyAccountQueryModel>(registered.Id, registered.Version -1)
                        .Email.Should().Be("newEmail2");

                    _serviceLocator.Resolve<IVersioningDocumentDbReader>()
                        .GetVersion<MyAccountQueryModel>(registered.Id, registered.Version - 2)
                        .Email.Should().Be("newEmail1");

                    _serviceLocator.Resolve<IVersioningDocumentDbReader>()
                        .GetVersion<MyAccountQueryModel>(registered.Id, registered.Version - 3)
                        .Email.Should().Be("originalEmail");
                }
            }
        }
    }

    namespace Domain
    {
        namespace UI
        {
            namespace QueryModels
            {
                public class MyAccountQueryModel : ISingleAggregateQueryModel
                {
                    public Guid Id { get; private set; }
                    internal string Email { get; set; }
                    internal string Password { get; set; }

                    public void SetId(Guid id)
                    {
                        Id = id;
                    }
                }

                public class AccountQueryModelGenerator : SingleAggregateQueryModelGenerator<AccountQueryModelGenerator, MyAccountQueryModel, IAccountEvent, IEventStoreReader>
                {
                    public AccountQueryModelGenerator(IEventStoreReader session) : base(session)
                    {
                        RegisterHandlers()
                            .For<IAccountEmailPropertyUpdatedEvent>(e => Model.Email = e.Email)
                            .For<IAccountPasswordPropertyUpdatedEvent>(e => Model.Password = e.Password);
                    }
                }
            }
        }


        class MyAccount : AggregateRoot<MyAccount,AccountEvent, IAccountEvent>
        {
            MyAccount():base(new DateTimeNowTimeSource())
            {
                RegisterEventAppliers()
                    .For<IAccountEmailPropertyUpdatedEvent>(e => Email = e.Email)
                    .For<IAccountPasswordPropertyUpdatedEvent>(e => Password = e.Password)
                    .For<IAccountDeletedEvent>(e => { });
            }

            public string Email { get; private set; }
            public string Password { get; private set; }

            public void ChangeEmail(string newEmail)
            {
                RaiseEvent(new EmailChangedEvent(newEmail));
            }

            public static MyAccount Register(IEventStoreSession aggregates, Guid accountId, string email, string password)
            {
                var registered = new MyAccount();
                registered.RaiseEvent(new AccountRegisteredEvent(accountId, email, password));
                aggregates.Save(registered);
                return registered;
            }

            public void Delete()
            {
                RaiseEvent(new AccountDeletedEvent());
            }
        }

        namespace Events
        {
            public interface IAccountEvent : IAggregateRootEvent {}
            abstract class AccountEvent : AggregateRootEvent, IAccountEvent
            {
                protected AccountEvent() { }
                public AccountEvent(Guid aggregateRootId):base(aggregateRootId)
                {
                }

            }

            interface IAccountRegisteredEvent
                : IAggregateRootCreatedEvent,
                    IAccountEmailPropertyUpdatedEvent,
                    IAccountPasswordPropertyUpdatedEvent {}

            interface IEmailChangedEvent : IAccountEvent,
                IAccountEmailPropertyUpdatedEvent {}

            interface IAccountDeletedEvent : IAccountEvent,
                IAggregateRootDeletedEvent
            {

            }

            namespace PropertyUpdated
            {
                interface IAccountEmailPropertyUpdatedEvent : IAccountEvent
                {
                    string Email { get; }
                }

                interface IAccountPasswordPropertyUpdatedEvent : IAccountEvent
                {
                    string Password { get; }
                }
            }

            namespace Implementation
            {
                class AccountRegisteredEvent : AccountEvent, IAccountRegisteredEvent
                {
                    public AccountRegisteredEvent(Guid accountId, String email, string password) : base(accountId)
                    {
                        Email = email;
                        Password = password;
                    }

                    public string Email { get; private set; }
                    public string Password { get; private set; }
                }

                class EmailChangedEvent : AccountEvent, IEmailChangedEvent
                {
                    public EmailChangedEvent(string newEmail) => Email = newEmail;

                    public string Email { get; private set; }
                }

                class AccountDeletedEvent : AccountEvent, IAccountDeletedEvent
                {

                }
            }
        }
    }
}
