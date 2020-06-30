using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using Composable.Contracts;
using Composable.Persistence.EventStore;
using Composable.System.Linq;

namespace Composable.Persistence.SqlServer.EventStore
{
    class SqlServerEventStorePersistenceLayerWriter : IEventStorePersistenceLayer.IWriter
    {
        const int PrimaryKeyViolationSqlErrorNumber = 2627;

        readonly SqlServerEventStoreConnectionManager _connectionManager;

        public SqlServerEventStorePersistenceLayerWriter
            (SqlServerEventStoreConnectionManager connectionManager) => _connectionManager = connectionManager;

        public void Insert(IReadOnlyList<EventDataRow> events)
        {
            using var connection = _connectionManager.OpenConnection();
            foreach(var data in events)
            {
                using var command = connection.CreateCommand();

                command.CommandText +=
                    $@"
INSERT {SqlServerEventTable.Name} With(READCOMMITTED, ROWLOCK) 
(       {SqlServerEventTable.Columns.AggregateId},  {SqlServerEventTable.Columns.InsertedVersion},  {SqlServerEventTable.Columns.ManualVersion}, {SqlServerEventTable.Columns.EventType},  {SqlServerEventTable.Columns.EventId},  {SqlServerEventTable.Columns.UtcTimeStamp},  {SqlServerEventTable.Columns.Event}) 
VALUES(@{SqlServerEventTable.Columns.AggregateId}, @{SqlServerEventTable.Columns.InsertedVersion}, @{SqlServerEventTable.Columns.ManualVersion}, @{SqlServerEventTable.Columns.EventType}, @{SqlServerEventTable.Columns.EventId}, @{SqlServerEventTable.Columns.UtcTimeStamp}, @{SqlServerEventTable.Columns.Event})";

                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.AggregateId, SqlDbType.UniqueIdentifier){Value = data.AggregateId });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.InsertedVersion, SqlDbType.Int) { Value = data.RefactoringInformation.InsertedVersion });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.EventType,SqlDbType.UniqueIdentifier){Value = data.EventType });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.EventId, SqlDbType.UniqueIdentifier) {Value = data.EventId});
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.UtcTimeStamp, SqlDbType.DateTime2) {Value = data.UtcTimeStamp});

                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.Event, SqlDbType.NVarChar, -1) {Value = data.EventJson});

                command.Parameters.Add(Nullable(new SqlParameter(SqlServerEventTable.Columns.ManualVersion, SqlDbType.Int) {Value = data.RefactoringInformation.ManualVersion}));

                try
                {
                    command.ExecuteNonQuery();
                }
                catch(SqlException e) when(e.Number == PrimaryKeyViolationSqlErrorNumber)
                {
                    throw new SqlServerEventStoreOptimisticConcurrencyException(e);
                }
            }
        }

        //urgent: Move almost all of this logic to the EventStore. Persistence layer should not implement the logic of refactoring.
        public void InsertRefactoringEvents(IReadOnlyList<EventDataRow> events)
        {
            // ReSharper disable PossibleInvalidOperationException
            var replacementGroup = events.Where(@event => @event.RefactoringInformation.Replaces.HasValue)
                                         .GroupBy(@event => @event.RefactoringInformation.Replaces!.Value)
                                         .SingleOrDefault();
            var insertBeforeGroup = events.Where(@event => @event.RefactoringInformation.InsertBefore.HasValue)
                                          .GroupBy(@event => @event.RefactoringInformation.InsertBefore!.Value)
                                          .SingleOrDefault();
            var insertAfterGroup = events.Where(@event => @event.RefactoringInformation.InsertAfter.HasValue)
                                         .GroupBy(@event => @event.RefactoringInformation.InsertAfter!.Value)
                                         .SingleOrDefault();
            // ReSharper restore PossibleInvalidOperationException

            Contract.Assert.That(Seq.Create(replacementGroup, insertBeforeGroup, insertAfterGroup).Where(@this => @this != null).Count() == 1,
                                 "Seq.Create(replacementGroup, insertBeforeGroup, insertAfterGroup).Where(@this => @this != null).Count() == 1");

            if (replacementGroup != null)
            {
                Contract.Assert.That(replacementGroup.All(@this => @this.RefactoringInformation.Replaces.HasValue && @this.RefactoringInformation.Replaces != Guid.Empty),
                                     "replacementGroup.All(@this => @this.Replaces.HasValue && @this.Replaces > 0)");
                var eventToReplace = LoadEventInsertedBeforeAndAfter(replacementGroup.Key);

                SaveRefactoringEventsWithinReadOrderRange(
                    newEvents: replacementGroup.ToArray(),
                    rangeStart: eventToReplace.EffectiveReadOrder,
                    rangeEnd: eventToReplace.NextReadOrder);
            }
            else if (insertBeforeGroup != null)
            {
                Contract.Assert.That(insertBeforeGroup.All(@this => @this.RefactoringInformation.InsertBefore.HasValue && @this.RefactoringInformation.InsertBefore.Value != Guid.Empty),
                                     "insertBeforeGroup.All(@this => @this.InsertBefore.HasValue && @this.InsertBefore.Value > 0)");
                var eventToInsertBefore = LoadEventInsertedBeforeAndAfter(insertBeforeGroup.Key);

                SaveRefactoringEventsWithinReadOrderRange(
                    newEvents: insertBeforeGroup.ToArray(),
                    rangeStart: eventToInsertBefore.PreviousReadOrder,
                    rangeEnd: eventToInsertBefore.EffectiveReadOrder);
            }
            else if (insertAfterGroup != null)
            {
                Contract.Assert.That(insertAfterGroup.All(@this => @this.RefactoringInformation.InsertAfter.HasValue && @this.RefactoringInformation.InsertAfter.Value != Guid.Empty),
                                     "insertAfterGroup.All(@this => @this.InsertAfter.HasValue && @this.InsertAfter.Value > 0)");
                var eventToInsertAfter = LoadEventInsertedBeforeAndAfter(insertAfterGroup.Key);

                SaveRefactoringEventsWithinReadOrderRange(
                    newEvents: insertAfterGroup.ToArray(),
                    rangeStart: eventToInsertAfter.EffectiveReadOrder,
                    rangeEnd: eventToInsertAfter.NextReadOrder);
            }

            FixManualVersions(events.First().AggregateId);
        }

        void SaveRefactoringEventsWithinReadOrderRange(EventDataRow[] newEvents, SqlDecimal rangeStart, SqlDecimal rangeEnd)
        {
            var readOrderIncrement = (rangeEnd - rangeStart) / (newEvents.Length + 1);

            using var connection = _connectionManager.OpenConnection();
            for(int index = 0; index < newEvents.Length; ++index)
            {
                var data = newEvents[index];
                using var command = connection.CreateCommand();

                command.CommandText +=
                    $@"
INSERT {SqlServerEventTable.Name} With(READCOMMITTED, ROWLOCK) 
(       {SqlServerEventTable.Columns.AggregateId},  {SqlServerEventTable.Columns.InsertedVersion},  {SqlServerEventTable.Columns.ManualVersion},  {SqlServerEventTable.Columns.ManualReadOrder},  {SqlServerEventTable.Columns.EventType},  {SqlServerEventTable.Columns.EventId},  {SqlServerEventTable.Columns.UtcTimeStamp},  {SqlServerEventTable.Columns.Event},  {SqlServerEventTable.Columns.InsertAfter}, {SqlServerEventTable.Columns.InsertBefore},  {SqlServerEventTable.Columns.Replaces}) 
VALUES(@{SqlServerEventTable.Columns.AggregateId}, @{SqlServerEventTable.Columns.InsertedVersion}, @{SqlServerEventTable.Columns.ManualVersion}, @{SqlServerEventTable.Columns.ManualReadOrder}, @{SqlServerEventTable.Columns.EventType}, @{SqlServerEventTable.Columns.EventId}, @{SqlServerEventTable.Columns.UtcTimeStamp}, @{SqlServerEventTable.Columns.Event}, @{SqlServerEventTable.Columns.InsertAfter},@{SqlServerEventTable.Columns.InsertBefore}, @{SqlServerEventTable.Columns.Replaces})
SET @{SqlServerEventTable.Columns.InsertionOrder} = SCOPE_IDENTITY();";

                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.AggregateId, SqlDbType.UniqueIdentifier){Value = data.AggregateId });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.InsertedVersion, SqlDbType.Int) { Value = data.RefactoringInformation.InsertedVersion });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.EventType,SqlDbType.UniqueIdentifier){Value = data.EventType });
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.EventId, SqlDbType.UniqueIdentifier) {Value = data.EventId});
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.UtcTimeStamp, SqlDbType.DateTime2) {Value = data.UtcTimeStamp});

                //Urgent: Change this to another data type. https://github.com/mlidbom/Composable/issues/46
                var manualReadOrder = rangeStart + (index + 1) * readOrderIncrement;
                if(!(manualReadOrder.IsNull || (manualReadOrder.Precision == 38 && manualReadOrder.Scale == 17)))
                {
                    throw new ArgumentException($"$$$$$$$$$$$$$$$$$$$$$$$$$ Found decimal with precision: {manualReadOrder.Precision} and scale: {manualReadOrder.Scale}", nameof(manualReadOrder));
                }
                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.ManualReadOrder, SqlDbType.Decimal) {Value = manualReadOrder});

                command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.Event, SqlDbType.NVarChar, -1) {Value = data.EventJson});

                command.Parameters.Add(Nullable(new SqlParameter(SqlServerEventTable.Columns.ManualVersion, SqlDbType.Int) {Value = data.RefactoringInformation.ManualVersion}));
                command.Parameters.Add(Nullable(new SqlParameter(SqlServerEventTable.Columns.InsertAfter, SqlDbType.UniqueIdentifier) {Value = data.RefactoringInformation.InsertAfter}));
                command.Parameters.Add(Nullable(new SqlParameter(SqlServerEventTable.Columns.InsertBefore, SqlDbType.UniqueIdentifier) {Value = data.RefactoringInformation.InsertBefore}));
                command.Parameters.Add(Nullable(new SqlParameter(SqlServerEventTable.Columns.Replaces, SqlDbType.UniqueIdentifier) {Value = data.RefactoringInformation.Replaces}));

                var identityParameter = new SqlParameter(SqlServerEventTable.Columns.InsertionOrder, SqlDbType.BigInt)
                                        {
                                            Direction = ParameterDirection.Output
                                        };

                command.Parameters.Add(identityParameter);

                try
                {
                    command.ExecuteNonQuery();
                }
                catch(SqlException e) when(e.Number == PrimaryKeyViolationSqlErrorNumber)
                {
                    throw new SqlServerEventStoreOptimisticConcurrencyException(e);
                }

                data.InsertionOrder = (long)identityParameter.Value;
            }
        }

        //Urgent: Do this logic in C# in the EventStore class. Persistence layer should only save the data, not implement logic that can be common for all persistence layers.
        void FixManualVersions(Guid aggregateId)
        {
            _connectionManager.UseCommand(
                command =>
                {
                    command.CommandText = SqlServerEventStore.SqlStatements.FixManualVersionsForAggregate;
                    command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.AggregateId, SqlDbType.UniqueIdentifier) {Value = aggregateId});
                    command.ExecuteNonQuery();
                });
        }

        static SqlDecimal ToCorrectPrecisionAndScale(SqlDecimal value) => SqlDecimal.ConvertToPrecScale(value, 38, 19);

        class EventOrderNeighborhood
        {
            long InsertionOrder { get; }
            public SqlDecimal EffectiveReadOrder { get; }
            public SqlDecimal PreviousReadOrder { get; }
            public SqlDecimal NextReadOrder { get; }

            public EventOrderNeighborhood(long insertionOrder, SqlDecimal effectiveReadOrder, SqlDecimal previousReadOrder, SqlDecimal nextReadOrder)
            {
                InsertionOrder = insertionOrder;
                EffectiveReadOrder = effectiveReadOrder;
                NextReadOrder = UseNextIntegerInsteadIfNullSinceThatMeansThisEventIsTheLastInTheEventStore(nextReadOrder);
                PreviousReadOrder = UseZeroInsteadIfNegativeSinceThisMeansThisIsTheFirstEventInTheEventStore(previousReadOrder);
            }

            static SqlDecimal UseZeroInsteadIfNegativeSinceThisMeansThisIsTheFirstEventInTheEventStore(SqlDecimal previousReadOrder) => previousReadOrder > 0 ? previousReadOrder : ToCorrectPrecisionAndScale(new SqlDecimal(0));

            SqlDecimal UseNextIntegerInsteadIfNullSinceThatMeansThisEventIsTheLastInTheEventStore(SqlDecimal nextReadOrder) => !nextReadOrder.IsNull ? nextReadOrder : ToCorrectPrecisionAndScale(new SqlDecimal(InsertionOrder + 1));
        }

        EventOrderNeighborhood LoadEventInsertedBeforeAndAfter(Guid insertionOrder)
        {
            var lockHintToMinimizeRiskOfDeadlocksByTakingUpdateLockOnInitialRead = "With(UPDLOCK, READCOMMITTED, ROWLOCK)";

            var selectStatement = $@"
SELECT  {SqlServerEventTable.Columns.InsertionOrder},
        {SqlServerEventTable.Columns.EffectiveReadOrder},        
        (select top 1 {SqlServerEventTable.Columns.EffectiveReadOrder} from {SqlServerEventTable.Name} e1 where e1.{SqlServerEventTable.Columns.EffectiveReadOrder} < {SqlServerEventTable.Name}.{SqlServerEventTable.Columns.EffectiveReadOrder} order by {SqlServerEventTable.Columns.EffectiveReadOrder} desc) PreviousReadOrder,
        (select top 1 {SqlServerEventTable.Columns.EffectiveReadOrder} from {SqlServerEventTable.Name} e1 where e1.{SqlServerEventTable.Columns.EffectiveReadOrder} > {SqlServerEventTable.Name}.{SqlServerEventTable.Columns.EffectiveReadOrder} order by {SqlServerEventTable.Columns.EffectiveReadOrder}) NextReadOrder
FROM    {SqlServerEventTable.Name} {lockHintToMinimizeRiskOfDeadlocksByTakingUpdateLockOnInitialRead} 
where {SqlServerEventTable.Columns.EventId} = @{SqlServerEventTable.Columns.EventId}";




            EventOrderNeighborhood? neighborhood = null;

            _connectionManager.UseCommand(
                command =>
                {
                    command.CommandText = selectStatement;
                    command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.EventId, SqlDbType.UniqueIdentifier) {Value = insertionOrder});
                    using var reader = command.ExecuteReader();
                    reader.Read();

                    neighborhood = new EventOrderNeighborhood(
                        insertionOrder: reader.GetInt64(0),
                        effectiveReadOrder: reader.GetSqlDecimal(1),
                        previousReadOrder: reader.GetSqlDecimal(2),
                        nextReadOrder: reader.GetSqlDecimal(3));
                });

            return Assert.Result.NotNull(neighborhood);
        }

        static SqlParameter Nullable(SqlParameter @this)
        {
            @this.IsNullable = true;
            @this.Direction = ParameterDirection.Input;
            if(@this.Value == null)
            {
                @this.Value = DBNull.Value;
            }
            return @this;
        }

        public void DeleteAggregate(Guid aggregateId)
        {
            _connectionManager.UseCommand(
                command =>
                {
                    command.CommandText +=
                        $"DELETE {SqlServerEventTable.Name} With(ROWLOCK) WHERE {SqlServerEventTable.Columns.AggregateId} = @{SqlServerEventTable.Columns.AggregateId}";
                    command.Parameters.Add(new SqlParameter(SqlServerEventTable.Columns.AggregateId, SqlDbType.UniqueIdentifier) {Value = aggregateId});
                    command.ExecuteNonQuery();
                });
        }
    }
}
