﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Composable.Persistence.DocumentDb;
using Composable.SystemCE.CollectionsCE.GenericCE;
using Composable.SystemCE.LinqCE;
using DocumentRow = Composable.Persistence.DocumentDb.IDocumentDbPersistenceLayer.WriteRow;
namespace Composable.Persistence.InMemory.DocumentDB
{
    //Performance: Write tests to expose lack of Transactional locks and transactional overlay and then implement it.
    class InMemoryDocumentDbPersistenceLayer : IDocumentDbPersistenceLayer
    {
        readonly Dictionary<string, List<DocumentRow>> _db = new Dictionary<string, List<DocumentRow>>(StringComparer.InvariantCultureIgnoreCase);
        readonly object _lockObject = new object();

        public void Add(IDocumentDbPersistenceLayer.WriteRow row)
        {
            lock (_lockObject)
            {
                if (Contains(row.TypeId, row.Id))
                {
                    throw new AttemptToSaveAlreadyPersistedValueException(row.Id, row.SerializedDocument);
                }
                _db.GetOrAddDefault(row.Id).Add(row);
            }
        }

        public bool TryGet(string idString, IReadonlySetCEx<Guid> acceptableTypeIds, bool useUpdateLock, [NotNullWhen(true)] out IDocumentDbPersistenceLayer.ReadRow? value)
        {
            lock (_lockObject)
            {
                value = null;
                if(!_db.TryGetValue(idString, out var matchesId))
                {
                    return false;
                }


                var found = matchesId.Where(@this => acceptableTypeIds.Contains(@this.TypeId) ).ToList();
                if(found.Any())
                {
                    var documentRow = found.Single();
                    value = new IDocumentDbPersistenceLayer.ReadRow(documentRow.TypeId, documentRow.SerializedDocument);
                    return true;
                }

                return false;
            }
        }

        public void Update(IReadOnlyList<DocumentRow> toUpdate)
        {
            lock (_lockObject)
            {
                foreach(var row in toUpdate)
                {
                    if (!TryGet(row.Id, new []{ row.TypeId }.ToSetCE(), useUpdateLock: false, out var existing)) throw new NoSuchDocumentException(row.Id, row.TypeId);
                    if (existing.SerializedDocument != row.SerializedDocument)
                    {
                        Remove(row.Id, new []{ row.TypeId }.ToSetCE());
                        Add(row);
                    }
                }
            }
        }

        public int Remove(string idstring, IReadonlySetCEx<Guid> acceptableTypes)
        {
            lock (_lockObject)
            {
                var removed = _db.GetOrAddDefault(idstring).RemoveWhere(@this => acceptableTypes.Contains(@this.TypeId));
                if (removed.None()) throw new NoSuchDocumentException(idstring, acceptableTypes.First());
                if (removed.Count > 1) throw new Exception("It really should be impossible to hit multiple documents with one Id, but apparently you just did it!");

                return 1;
            }
        }

        public IEnumerable<Guid> GetAllIds(IReadonlySetCEx<Guid> acceptableTypes)
        {
            var typeIds = new HashSet<Guid>(acceptableTypes);
            lock (_lockObject)
            {
                return _db
                      .SelectMany(@this => @this.Value)
                      .Where(@this => typeIds.Contains(@this.TypeId))
                      .Select(@this =>
                       {
#pragma warning disable CA1806 // Do not ignore method results
                           Guid.TryParse(@this.Id, out var id);
#pragma warning restore CA1806 // Do not ignore method results
                           return id;
                       })
                      .Where(@this => @this != Guid.Empty)
                      .ToList();
            }
        }

        public IReadOnlyList<IDocumentDbPersistenceLayer.ReadRow> GetAll(IEnumerable<Guid> ids, IReadonlySetCEx<Guid> acceptableTypes)
        {
            lock (_lockObject)
            {
                return _db
                      .SelectMany(@this => @this.Value)
                      .Where(@this =>  acceptableTypes.Contains(@this.TypeId) && Guid.TryParse(@this.Id, out var myId) && ids.Contains(myId))
                      .Select(@this => new IDocumentDbPersistenceLayer.ReadRow(@this.TypeId, @this.SerializedDocument))
                      .ToList();
            }
        }

        public IReadOnlyList<IDocumentDbPersistenceLayer.ReadRow> GetAll(IReadonlySetCEx<Guid> acceptableTypes)
        {
            var typeIds = new HashSet<Guid>(acceptableTypes);
            lock (_lockObject)
            {
                return _db
                      .SelectMany(@this => @this.Value)
                      .Where(@this => typeIds.Contains(@this.TypeId))
                      .Select(@this => new IDocumentDbPersistenceLayer.ReadRow(@this.TypeId, @this.SerializedDocument))
                      .ToList();
            }
        }

        bool Contains(Guid type, string id) => TryGet(id, new[]{ type }.ToSetCE(), false, out _);
    }
}
