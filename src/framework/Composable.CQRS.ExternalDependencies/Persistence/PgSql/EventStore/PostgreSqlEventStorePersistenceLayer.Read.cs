using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Composable.Persistence.Common.EventStore;
using Composable.Persistence.EventStore;
using Composable.Persistence.EventStore.PersistenceLayer;
using Composable.Persistence.PgSql.SystemExtensions;
using Composable.System;
using Npgsql;
using NpgsqlTypes;
using C = Composable.Persistence.Common.EventStore.EventTable.Columns;

namespace Composable.Persistence.PgSql.EventStore
{
    partial class PgSqlEventStorePersistenceLayer : IEventStorePersistenceLayer
    {
        readonly PgSqlEventStoreConnectionManager _connectionManager;

        public PgSqlEventStorePersistenceLayer(PgSqlEventStoreConnectionManager connectionManager) => _connectionManager = connectionManager;

        static string CreateSelectClause() => InternalSelect();

        //Urgent: Find PgSql equivalents
        //var lockHint = takeWriteLock ? "With(UPDLOCK, READCOMMITTED, ROWLOCK)" : "With(READCOMMITTED, ROWLOCK)";
        static string CreateLockHint(bool takeWriteLock) => takeWriteLock ? "FOR UPDATE" : "";

        static string InternalSelect(int? top = null)
        {
            var topClause = top.HasValue ? $"TOP {top.Value} " : "";

            return $@"
SELECT {topClause} 
{C.EventType}, {C.Event}, {C.AggregateId}, {C.EffectiveVersion}, {C.EventId}, {C.UtcTimeStamp}, {C.InsertionOrder}, {C.TargetEvent}, {C.RefactoringType}, {C.InsertedVersion}, cast({C.EffectiveOrder} as varchar) as CharEffectiveOrder --The as is required, or Postgre sorts by this column when we ask it to sort by EffectiveOrder.
FROM {EventTable.Name}";
        }

        static EventDataRow ReadDataRow(NpgsqlDataReader eventReader)
        {
            return new EventDataRow(
                eventType: Guid.Parse(eventReader.GetString(0)),
                eventJson: eventReader.GetString(1),
                eventId: Guid.Parse(eventReader.GetString(4)),
                aggregateVersion: eventReader.GetInt32(3),
                aggregateId: Guid.Parse(eventReader.GetString(2)),
                //Without this the datetime will be DateTimeKind.Unspecified and will not convert correctly into Local time....
                utcTimeStamp: DateTime.SpecifyKind(eventReader.GetDateTime(5), DateTimeKind.Utc),
                storageInformation: new AggregateEventStorageInformation()
                                        {
                                            ReadOrder = ReadOrder.Parse(eventReader.GetString(10)),
                                            InsertedVersion = eventReader.GetInt32(9),
                                            EffectiveVersion = eventReader.GetInt32(3),
                                            RefactoringInformation = (eventReader[7] as string, eventReader[8] as short?)switch
                                            {
                                                (null, null) => null,
                                                (string targetEvent, short type) => new AggregateEventRefactoringInformation(Guid.Parse(targetEvent), (AggregateEventRefactoringType)type),
                                                (_, _) => throw new Exception("Should not be possible to get here")
                                            }
                                        }
            );
        }

        public IReadOnlyList<EventDataRow> GetAggregateHistory(Guid aggregateId, bool takeWriteLock, int startAfterInsertedVersion = 0)
        {
            IReadOnlyList<EventDataRow> GetHistory()
            {
                return _connectionManager.UseCommand(suppressTransactionWarning: true,
                                                     command => command.SetCommandText($@"

{CreateSelectClause()} 
WHERE {C.AggregateId} = @{C.AggregateId}
    AND {C.InsertedVersion} >= @CachedVersion
    AND {C.EffectiveVersion} > 0
ORDER BY {C.EffectiveOrder} ASC;
")
                                                                       .AddParameter(C.AggregateId, aggregateId)
                                                                       .AddParameter("CachedVersion", startAfterInsertedVersion)
                                                                       .ExecuteReaderAndSelect(ReadDataRow)
                                                                       .SkipWhile(@this => @this.StorageInformation.InsertedVersion <= startAfterInsertedVersion)
                                                                       .ToList());
            }

            if(takeWriteLock)
            {
                //Performance: Find a way of doing this so that it does not involve two round trips to the server. If running as single-instance we can use in-memory transactional locking such as in the InMemory Persistence Layer to avoid needing this.
                //Without this hack PostgreSql does not correctly serialize access to aggregates and odds are you would get a lot of failed transactions if an aggregate is "popular"
                //Pages that led to the below hack: https://tinyurl.com/y7nef75p, https://tinyurl.com/y7c63cny, https://tinyurl.com/y75qlwar
                _connectionManager.UseCommand(command => command.SetCommandText($"SET enable_seqscan=off;select {C.AggregateId} from AggregateLock where AggregateId = @{C.AggregateId} for update;SET enable_seqscan=on;")
                                                                                        .AddParameter(C.AggregateId, aggregateId)
                                                                                        .ExecuteNonQuery());

                //We took care of the locking on the line above. Since events are Append only this lock is sufficient. Suppressing the current transaction keeps PostgreSql from incorrectly detecting a collision and failing our transactions.
                using var ignore = new TransactionScope(TransactionScopeOption.Suppress);
                return GetHistory();
            } else
            {
                return GetHistory();
            }
        }

        public IEnumerable<EventDataRow> StreamEvents(int batchSize)
        {
            ReadOrder lastReadEventReadOrder = default;
            int fetchedInThisBatch;
            do
            {
                var historyData = _connectionManager.UseCommand(suppressTransactionWarning: true,
                                                                command =>
                                                                {
                                                                    var commandText = $@"
{CreateSelectClause()} 
WHERE {C.EffectiveOrder}  > CAST(@{C.EffectiveOrder} AS {EventTable.ReadOrderType})
    AND {C.EffectiveVersion} > 0
ORDER BY {C.EffectiveOrder} ASC
LIMIT {batchSize}";
                                                                    return command.SetCommandText(commandText)
                                                                                  .AddParameter(C.EffectiveOrder, NpgsqlDbType.Varchar, lastReadEventReadOrder.ToString())
                                                                                  .ExecuteReaderAndSelect(ReadDataRow)
                                                                                  .ToList();
                                                                });
                if(historyData.Any())
                {
                    lastReadEventReadOrder = historyData[^1].StorageInformation.ReadOrder!.Value;
                }

                //We do not yield while reading from the reader since that may cause code to run that will cause another sql call into the same connection. Something that throws an exception unless you use an unusual and non-recommended connection string setting.
                foreach(var eventDataRow in historyData)
                {
                    yield return eventDataRow;
                }

                fetchedInThisBatch = historyData.Count;
            } while(!(fetchedInThisBatch < batchSize));
        }

        public IReadOnlyList<CreationEventRow> ListAggregateIdsInCreationOrder()
        {
            return _connectionManager.UseCommand(suppressTransactionWarning: true,
                                                 action: command => command.SetCommandText($@"
SELECT {C.AggregateId}, {C.EventType} 
FROM {EventTable.Name} 
WHERE {C.EffectiveVersion} = 1 
ORDER BY {C.EffectiveOrder} ASC")
                                                                            //Urgent: Check out how to deal with Guids
                                                                           .ExecuteReaderAndSelect(reader => new CreationEventRow(aggregateId: Guid.Parse(reader.GetString(0)), typeId: Guid.Parse(reader.GetString(1)))));
        }
    }
}