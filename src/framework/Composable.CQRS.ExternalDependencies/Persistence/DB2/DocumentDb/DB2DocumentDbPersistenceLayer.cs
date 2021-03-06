using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Composable.Persistence.Common.AdoCE;
using Composable.Persistence.DocumentDb;
using Composable.Persistence.DB2.SystemExtensions;
using Composable.SystemCE;
using Composable.SystemCE.CollectionsCE.GenericCE;
using Composable.SystemCE.ThreadingCE.ResourceAccess;
using IBM.Data.DB2.Core;
using Schema = Composable.Persistence.DocumentDb.IDocumentDbPersistenceLayer.DocumentTableSchemaStrings;

namespace Composable.Persistence.DB2.DocumentDb
{
    partial class DB2DocumentDbPersistenceLayer : IDocumentDbPersistenceLayer
    {
        readonly IDB2ConnectionPool _connectionPool;
        readonly SchemaManager _schemaManager;
        bool _initialized;

        internal DB2DocumentDbPersistenceLayer(IDB2ConnectionPool connectionPool)
        {
            _schemaManager = new SchemaManager(connectionPool);
            _connectionPool = connectionPool;
        }

        public void Update(IReadOnlyList<IDocumentDbPersistenceLayer.WriteRow> toUpdate)
        {
            EnsureInitialized();
            _connectionPool.UseConnection(connection =>
            {
                foreach(var writeRow in toUpdate)
                {
                    connection.UseCommand(
                        command => command.SetCommandText($"UPDATE {Schema.TableName} SET {Schema.Value} = @{Schema.Value}, {Schema.Updated} = @{Schema.Updated} WHERE {Schema.Id} = @{Schema.Id} AND {Schema.ValueTypeId} = @{Schema.ValueTypeId}")
                                          .AddVarcharParameter(Schema.Id, 500, writeRow.Id)
                                          .AddParameter(Schema.Updated, writeRow.UpdateTime)
                                          .AddParameter(Schema.ValueTypeId, writeRow.TypeId)
                                          .AddNClobParameter(Schema.Value, writeRow.SerializedDocument)
                                          .ExecuteNonQuery());
                }
            });
        }

        public bool TryGet(string idString, IReadonlySetCEx<Guid> acceptableTypeIds, bool useUpdateLock, [NotNullWhen(true)] out IDocumentDbPersistenceLayer.ReadRow? document)
        {
            EnsureInitialized();

            //Performance: check if db2 does array(or whatever it might be called) parameters. Same for other storage providers. Properly parameterizing this might significantly help performance
            var documents = _connectionPool.UseCommand(
                command => command.SetCommandText($@"
SELECT {Schema.Value}, {Schema.ValueTypeId} FROM {Schema.TableName} {UseUpdateLock(useUpdateLock)} 
WHERE {Schema.Id}=@{Schema.Id} AND {Schema.ValueTypeId} {TypeInClause(acceptableTypeIds)}")
                                  .AddVarcharParameter(Schema.Id, 500, idString)
                                  .ExecuteReaderAndSelect(reader => new IDocumentDbPersistenceLayer.ReadRow(reader.GetGuidFromString(1), reader.GetString(0))));
            if(documents.Count < 1)
            {
                document = null;
                return false;
            }

            document = documents[0];

            return true;
        }

        public void Add(IDocumentDbPersistenceLayer.WriteRow row)
        {
            EnsureInitialized();
            try
            {
                _connectionPool.UseCommand(command =>
                {
                    command.SetCommandText($@"INSERT INTO {Schema.TableName}({Schema.Id}, {Schema.ValueTypeId}, {Schema.Value}, {Schema.Created}, {Schema.Updated}) VALUES(@{Schema.Id}, @{Schema.ValueTypeId}, @{Schema.Value}, @{Schema.Created}, @{Schema.Updated})")
                           .AddVarcharParameter(Schema.Id, 500, row.Id)
                           .AddParameter(Schema.ValueTypeId, row.TypeId)
                           .AddParameter(Schema.Created, row.UpdateTime)
                           .AddParameter(Schema.Updated, row.UpdateTime)
                           .AddNClobParameter(Schema.Value, row.SerializedDocument)
                           .ExecuteNonQuery();
                });
            }
            catch(DB2Exception exception)when(SqlExceptions.DB2.IsUniqueConstraintViolation_TODO(exception))
            {
                throw new AttemptToSaveAlreadyPersistedValueException(row.Id, row.SerializedDocument);
            }
        }

        public int Remove(string idString, IReadonlySetCEx<Guid> acceptableTypes)
        {
            EnsureInitialized();
            return _connectionPool.UseCommand(
                command =>
                    command.SetCommandText($@"DELETE FROM {Schema.TableName} WHERE {Schema.Id} = @{Schema.Id} AND {Schema.ValueTypeId} {TypeInClause(acceptableTypes)}")
                           .AddVarcharParameter(Schema.Id, 500, idString)
                           .ExecuteNonQuery());
        }

        public IEnumerable<Guid> GetAllIds(IReadonlySetCEx<Guid> acceptableTypes)
        {
            EnsureInitialized();
            return _connectionPool.UseCommand(
                command => command.SetCommandText($@"SELECT {Schema.Id} FROM {Schema.TableName} WHERE {Schema.ValueTypeId} {TypeInClause(acceptableTypes)}")
                                  .ExecuteReaderAndSelect(reader => reader.GetGuidFromString(0)));
        }

        public IReadOnlyList<IDocumentDbPersistenceLayer.ReadRow> GetAll(IEnumerable<Guid> ids, IReadonlySetCEx<Guid> acceptableTypes)
        {
            EnsureInitialized();
            return _connectionPool.UseCommand(
                command => command.SetCommandText($@"SELECT {Schema.Id}, {Schema.Value}, {Schema.ValueTypeId} FROM {Schema.TableName} WHERE {Schema.ValueTypeId} {TypeInClause(acceptableTypes)} 
                                   AND {Schema.Id} IN('" + ids.Select(id => id.ToString()).Join("','") + "')")
                                  .ExecuteReaderAndSelect(reader => new IDocumentDbPersistenceLayer.ReadRow(reader.GetGuidFromString(2), reader.GetString(1))));
        }

        public IReadOnlyList<IDocumentDbPersistenceLayer.ReadRow> GetAll(IReadonlySetCEx<Guid> acceptableTypes)
        {
            EnsureInitialized();
            return _connectionPool.UseCommand(
                command => command.SetCommandText($@"SELECT {Schema.Id}, {Schema.Value}, {Schema.ValueTypeId} FROM {Schema.TableName} WHERE {Schema.ValueTypeId} {TypeInClause(acceptableTypes)}")
                                  .ExecuteReaderAndSelect(reader => new IDocumentDbPersistenceLayer.ReadRow(reader.GetGuidFromString(2), reader.GetString(1))));
        }

        static string TypeInClause(IEnumerable<Guid> acceptableTypeIds) { return "IN( '" + acceptableTypeIds.Select(guid => guid.ToString()).Join("', '") + "')"; }

        // ReSharper disable once UnusedParameter.Local
        static string UseUpdateLock(bool _) => ""; // useUpdateLock ? "With(UPDLOCK, ROWLOCK)" : "";


        readonly MonitorCE _monitor = MonitorCE.WithDefaultTimeout();
        void EnsureInitialized() => _monitor.Update(() =>
        {
            if(!_initialized)
            {
                _schemaManager.EnsureInitialized();
                _initialized = true;
            }
        });
    }
}
