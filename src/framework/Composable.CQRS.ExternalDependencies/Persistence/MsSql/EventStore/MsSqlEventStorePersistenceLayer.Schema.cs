﻿using Composable.Persistence.Common.EventStore;
using Composable.Persistence.EventStore;
using Composable.Persistence.EventStore.PersistenceLayer;
using Composable.Persistence.MsSql.SystemExtensions;
using Composable.System.Transactions;
using C=Composable.Persistence.Common.EventStore.EventTable.Columns;

namespace Composable.Persistence.MsSql.EventStore
{
    partial class MsSqlEventStorePersistenceLayer : IEventStorePersistenceLayer
    {

        bool _initialized;

        public void SetupSchemaIfDatabaseUnInitialized() => TransactionScopeCe.SuppressAmbientAndExecuteInNewTransaction(() =>
        {
            if(!_initialized)
            {
                _connectionManager.UseCommand(command=> command.ExecuteNonQuery($@"
IF NOT EXISTS(SELECT NAME FROM sys.tables WHERE name = '{EventTable.Name}')
BEGIN
    CREATE TABLE dbo.{EventTable.Name}(
        {C.InsertionOrder} bigint IDENTITY(1,1) NOT NULL,
        {C.AggregateId} uniqueidentifier NOT NULL,  
        {C.UtcTimeStamp} datetime2 NOT NULL,   
        {C.EventType} uniqueidentifier NOT NULL,    
        {C.Event} nvarchar(max) NOT NULL,
        {C.EventId} uniqueidentifier NOT NULL,
        {C.InsertedVersion} int NOT NULL,
        {C.SqlInsertTimeStamp} datetime2 default SYSUTCDATETIME(),
        {C.TargetEvent} uniqueidentifier null,
        {C.RefactoringType} tinyint null,
        {C.ReadOrder} bigint null,
        {C.ReadOrderOrderOffset} bigint null,
        {C.EffectiveOrder} {EventTable.ReadOrderType} null,    
        {C.EffectiveVersion} int NULL,

        CONSTRAINT PK_{EventTable.Name} PRIMARY KEY CLUSTERED 
        (
            {C.AggregateId} ASC,
            {C.InsertedVersion} ASC
        )WITH (ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = OFF),

        CONSTRAINT IX_{EventTable.Name}_Unique_{C.EventId} UNIQUE ( {C.EventId} ),
        CONSTRAINT IX_{EventTable.Name}_Unique_{C.InsertionOrder} UNIQUE ( {C.InsertionOrder} ),

        CONSTRAINT FK_{EventTable.Name}_{C.TargetEvent} FOREIGN KEY ( {C.TargetEvent} ) 
            REFERENCES {EventTable.Name} ({C.EventId}) 
    )

        CREATE NONCLUSTERED INDEX IX_{EventTable.Name}_{C.EffectiveOrder} ON dbo.{EventTable.Name}
            ({C.EffectiveOrder}, {C.EffectiveVersion})
            INCLUDE ({C.EventType}, {C.InsertionOrder})
END 
"));

                _initialized = true;
            }
        });
    }
}