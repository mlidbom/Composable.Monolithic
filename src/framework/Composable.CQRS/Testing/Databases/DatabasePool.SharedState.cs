﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Composable.Contracts;
using Composable.Serialization;
using Composable.SystemCE;
using Composable.SystemCE.LinqCE;
using JetBrains.Annotations;

namespace Composable.Testing.Databases
{
    partial class DatabasePool
    {
        [UsedImplicitly] protected class SharedState : BinarySerialized<SharedState>
        {
            const int CleanDatabaseNumberTarget = 10;
            protected override IEnumerable<MemberGetterSetter> CreateGetterSetters() => new[] {GetterSetter.ForBinarySerializableList(@this => @this._databases, (@this, value) => @this._databases = value)};

            List<Database> _databases = new List<Database>();
            internal IReadOnlyList<Database> Databases => _databases;

            internal bool IsEmpty => _databases.Count == 0;

            internal bool IsValid
            {
                get
                {
                    if(_databases.Count != DatabasePool.NumberOfDatabases)
                    {
                        return false;
                    }

                    for(var i = 1; i <= _databases.Count; i++)
                    {
                        if(i != _databases[i - 1].Id)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            internal bool TryReserve(string reservationName, Guid poolId, TimeSpan reservationLength, [NotNullWhen(true)]out Database? reserved)
            {
                CollectGarbage();

                reserved = CleanUnReserved.FirstOrDefault() ?? UnReserved.FirstOrDefault();

                reserved?.Reserve(reservationName, poolId, reservationLength);
                return reserved != null;
            }


            IEnumerable<Database> DirtyUnReserved => UnReserved.Where(db => !db.IsClean);

            IEnumerable<Database> CleanUnReserved => UnReserved.Where(db => db.IsClean);

            IEnumerable<Database> UnReserved => _databases.Where(db => !db.IsReserved)
                                                          .OrderByDescending(db => db.ExpirationDateTime);//Reusing lately used databases helps performance in a pretty big way, disk cache, connection pool etc

            internal IEnumerable<Database> ReserveDatabasesForCleaning(Guid poolId)
            {
                CollectGarbage();
                var databasesToClean = Math.Max(CleanDatabaseNumberTarget - CleanUnReserved.Count(), 0);

                return DirtyUnReserved
                                .Take(databasesToClean)
                                .Select(@this => @this.Mutate(db => db.Reserve(reservationName: Guid.NewGuid().ToString(),
                                                                               poolId: poolId,
                                                                               reservationLength: 10.Minutes())))
                                .ToList();
            }

            internal void ReleaseClean(string reservationName)
            {
                var existing = Databases.SingleOrDefault(@this => @this.ReservationName == reservationName);
                Assert.Argument.Assert(existing.IsReserved);
                existing.Release();
                existing.Clean();
            }

            void CollectGarbage() => Databases.Where(db => db.ShouldBeReleased)
                                              .ForEach( db => db.Release());

            internal void ReleaseReservationsFor(Guid poolId) => DatabasesReservedBy(poolId).ForEach(db => db.Release());

            internal IReadOnlyList<Database> DatabasesReservedBy(Guid poolId) => _databases.Where(db => db.IsReserved && db.ReservedByPoolId == poolId).ToList();

            internal Database Insert()
            {
                var database = new Database(_databases.Count + 1);
                _databases.Add(database);
                return database;
            }

            internal void Reset() { _databases.Clear(); }
        }
    }
}
