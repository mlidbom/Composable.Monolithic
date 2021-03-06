﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Composable.Messaging.Buses.Implementation;
using Composable.SystemCE.ThreadingCE.ResourceAccess;
using Composable.SystemCE.TransactionsCE;
// ReSharper disable MemberCanBePrivate.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Composable.Persistence.InMemory.ServiceBus
{
    class InMemoryInboxPersistenceLayer : IServiceBusPersistenceLayer.IInboxPersistenceLayer
    {
        readonly IThreadShared<Implementation> _implementation = ThreadShared.WithDefaultTimeout(new Implementation());

        public void SaveMessage(Guid messageId, Guid typeId, string serializedMessage) => _implementation.Update(@this => @this.SaveMessage(messageId, typeId, serializedMessage));

        public void MarkAsSucceeded(Guid messageId)
            => Transaction.Current!.AddCommitTasks(() => _implementation.Update(@this => @this.MarkAsSucceeded(messageId)));

        public int RecordException(Guid messageId, string exceptionStackTrace, string exceptionMessage, string exceptionType)
            => _implementation.Update(@this => @this.RecordException(messageId, exceptionStackTrace, exceptionMessage, exceptionType));

        public int MarkAsFailed(Guid messageId) => _implementation.Update(@this => @this.MarkAsFailed(messageId));

        public Task InitAsync() => _implementation.Update(@this => @this.InitAsync());

        class Implementation : IServiceBusPersistenceLayer.IInboxPersistenceLayer
        {
            readonly List<Row> _rows = new List<Row>();

            public void SaveMessage(Guid messageId, Guid typeId, string serializedMessage) => _rows.Add(new Row(messageId, typeId, serializedMessage));

            public void MarkAsSucceeded(Guid messageId) => _rows.Single(@this => @this.MessageId == messageId).Status = Inbox.MessageStatus.Succeeded;

            public int RecordException(Guid messageId, string exceptionStackTrace, string exceptionMessage, string exceptionType)
            {
                var message = _rows.Single(@this => @this.MessageId == messageId);
                message.Status = Inbox.MessageStatus.Succeeded;
                message.ExceptionMessage = exceptionMessage;
                message.ExceptionStackTrace = exceptionStackTrace;
                message.ExceptionType = exceptionType;
                return 1;
            }

            public int MarkAsFailed(Guid messageId)
            {
                _rows.Single(@this => @this.MessageId == messageId).Status = Inbox.MessageStatus.Failed;
                return 1;
            }

            public Task InitAsync() => Task.CompletedTask;

            class Row
            {
                public Row(Guid messageId, Guid typeId, string serializedMessage)
                {
                    MessageId = messageId;
                    TypeId = typeId;
                    SerializedMessage = serializedMessage;
                }

                public Guid MessageId { get; }
                public Guid TypeId { get; }
                public string SerializedMessage { get; }

                public Inbox.MessageStatus Status { get; set; } = Inbox.MessageStatus.UnHandled;

                public string ExceptionMessage { get; set; } = string.Empty;
                public string ExceptionType { get; set;} = string.Empty;
                public string ExceptionStackTrace { get; set; } = string.Empty;
            }
        }
    }
}
