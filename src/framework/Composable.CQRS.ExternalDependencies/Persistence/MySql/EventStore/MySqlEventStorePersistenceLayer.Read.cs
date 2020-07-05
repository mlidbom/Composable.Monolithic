using System;
using System.Collections.Generic;
using System.Linq;
using Composable.Persistence.Common.EventStore;
using Composable.Persistence.EventStore;
using Composable.Persistence.MySql.SystemExtensions;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using C = Composable.Persistence.Common.EventStore.EventTable.Columns;

namespace Composable.Persistence.MySql.EventStore
{
    partial class MySqlEventStorePersistenceLayer : IEventStorePersistenceLayer
    {
        readonly MySqlEventStoreConnectionManager _connectionManager;

        public MySqlEventStorePersistenceLayer(MySqlEventStoreConnectionManager connectionManager) => _connectionManager = connectionManager;

        static string CreateSelectClause(bool takeWriteLock) => InternalSelect(takeWriteLock: takeWriteLock);
        static string CreateSelectTopClause(int top, bool takeWriteLock) => InternalSelect(top: top, takeWriteLock: takeWriteLock);

        static string InternalSelect(bool takeWriteLock, int? top = null)
        {
            var topClause = top.HasValue ? $"TOP {top.Value} " : "";
            //todo: Ensure that READCOMMITTED is truly sane here. If so add a comment describing why and why using it is a good idea.
            var lockHint = takeWriteLock ? "With(UPDLOCK, READCOMMITTED, ROWLOCK)" : "With(READCOMMITTED, ROWLOCK)";

            return $@"
SELECT {topClause} 
{C.EventType}, {C.Event}, {C.AggregateId}, {C.EffectiveVersion}, {C.EventId}, {C.UtcTimeStamp}, {C.InsertionOrder}, {C.InsertAfter}, {C.InsertBefore}, {C.Replaces}, {C.InsertedVersion}, {C.EffectiveOrder}
FROM {EventTable.Name} {lockHint} ";
        }

        static EventDataRow ReadDataRow(MySqlDataReader eventReader)
        {
            throw new NotImplementedException();
            return new EventDataRow(
                eventType: eventReader.GetGuid(0),
                eventJson: eventReader.GetString(1),
                eventId: eventReader.GetGuid(4),
                aggregateVersion: eventReader.GetInt32(3),
                aggregateId: eventReader.GetGuid(2),
                //Without this the datetime will be DateTimeKind.Unspecified and will not convert correctly into Local time....
                utcTimeStamp: DateTime.SpecifyKind(eventReader.GetDateTime(5), DateTimeKind.Utc),
                refactoringInformation: new AggregateEventRefactoringInformation()
                                        {
                                            //urgent:implement
                                            //EffectiveOrder = IEventStorePersistenceLayer.ReadOrder.FromSqlDecimal(eventReader.GetMySqlDecimal(11)),
                                            InsertedVersion = eventReader.GetInt32(10),
                                            EffectiveVersion = eventReader.GetInt32(3),
                                            InsertAfter = eventReader[7] as Guid?,
                                            InsertBefore = eventReader[8] as Guid?,
                                            Replaces = eventReader[9] as Guid?
                                        }
            );
        }

        public IReadOnlyList<EventDataRow> GetAggregateHistory(Guid aggregateId, bool takeWriteLock, int startAfterInsertedVersion = 0) =>
            _connectionManager.UseCommand(suppressTransactionWarning: !takeWriteLock,
                                          command => command.SetCommandText($@"
{CreateSelectClause(takeWriteLock)} 
WHERE {C.AggregateId} = @{C.AggregateId}
    AND {C.InsertedVersion} > @CachedVersion
    AND {C.EffectiveVersion} > 0
ORDER BY {C.EffectiveOrder} ASC")
                                                            .AddParameter(C.AggregateId, aggregateId)
                                                            .AddParameter("CachedVersion", startAfterInsertedVersion)
                                                            .ExecuteReaderAndSelect(ReadDataRow)
                                                            .ToList());

        public IEnumerable<EventDataRow> StreamEvents(int batchSize)
        {
            MySqlDecimal lastReadEventReadOrder = default;
            int fetchedInThisBatch;
            do
            {
                var historyData = _connectionManager.UseCommand(suppressTransactionWarning: true,
                                                                command => command.SetCommandText($@"
{CreateSelectTopClause(batchSize, takeWriteLock: false)} 
WHERE {C.EffectiveOrder}  > @{C.EffectiveOrder}
    AND {C.EffectiveVersion} > 0
ORDER BY {C.EffectiveOrder} ASC")
                                                                                  .AddParameter(C.EffectiveOrder, MySqlDbType.Decimal, lastReadEventReadOrder)
                                                                                  .ExecuteReaderAndSelect(ReadDataRow)
                                                                                  .ToList());
                if(historyData.Any())
                {
                    //urgent:implement
                    throw new NotImplementedException();
                    //lastReadEventReadOrder = historyData[^1].RefactoringInformation.EffectiveOrder!.Value.ToSqlDecimal();
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
                                                                           .ExecuteReaderAndSelect(reader => new CreationEventRow(aggregateId: reader.GetGuid(0), typeId: reader.GetGuid(1))));
        }
    }
}