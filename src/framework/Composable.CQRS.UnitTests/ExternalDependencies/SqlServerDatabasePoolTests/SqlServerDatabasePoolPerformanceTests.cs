﻿using System;
using Composable.Logging;
using Composable.Persistence.SqlServer.SystemExtensions;
using Composable.Persistence.SqlServer.Testing.Databases;
using Composable.System;
using Composable.Testing;
using Composable.Testing.Performance;
using NCrunch.Framework;
using NUnit.Framework;

namespace Composable.Tests.ExternalDependencies.SqlServerDatabasePoolTests
{
    [TestFixture, Performance, Serial]
    public class SqlServerDatabasePoolPerformanceTests
    {
        [OneTimeSetUp]public void WarmUpCache()
        {
            using var pool = new SqlServerDatabasePool();
            pool.ConnectionStringFor("3A0051EF-392B-46E2-AAB3-564C27138C94");
        }

        [Test]
        public void Single_thread_can_reserve_and_release_10_identically_named_databases_in_300_milliseconds()
        {
            var dbName = "74EA37DF-03CE-49C4-BDEC-EAD40FAFB3A1";

            TimeAsserter.Execute(
                action:
                () =>
                {
                    using var manager = new SqlServerDatabasePool();
                    manager.SetLogLevel(LogLevel.Warning);
                    manager.ConnectionStringFor(dbName);
                },
                iterations: 10,
                maxTotal: 300.Milliseconds());
        }

        [Test]
        public void Multiple_threads_can_reserve_and_release_10_identically_named_databases_in_70_milliseconds()
        {
            var dbName = "EB82270F-E0BA-49F7-BC09-79AE95BA109F";

            TimeAsserter.ExecuteThreaded(
                action:
                () =>
                {
                    using var manager = new SqlServerDatabasePool();
                    manager.SetLogLevel(LogLevel.Warning);
                    manager.ConnectionStringFor(dbName);
                },
                iterations: 10,
                timeIndividualExecutions: true,
                maxTotal: 70.Milliseconds());
        }

        [Test]
        public void Multiple_threads_can_reserve_and_release_10_differently_named_databases_in_300_milliseconds()
        {
            SqlServerDatabasePool manager = null;

            TimeAsserter.ExecuteThreaded(
                setup: () =>
                       {
                           manager = new SqlServerDatabasePool();
                           manager.SetLogLevel(LogLevel.Warning);
                           manager.ConnectionStringFor("fake_to_force_creation_of_manager_database");
                       },
                tearDown: () => manager.Dispose(),
                action: () => manager.ConnectionStringFor(Guid.NewGuid().ToString()),
                iterations: 10,
                maxTotal: 300.Milliseconds()
            );
        }

        [Test]
        public void Single_thread_can_reserve_and_release_10_differently_named_databases_in_300_milliseconds()
        {
            SqlServerDatabasePool manager = null;

            TimeAsserter.Execute(
                setup: () =>
                       {
                           manager = new SqlServerDatabasePool();
                           manager.SetLogLevel(LogLevel.Warning);
                           manager.ConnectionStringFor("fake_to_force_creation_of_manager_database");
                       },
                tearDown: () => manager.Dispose(),
                action: () => manager.ConnectionStringFor(Guid.NewGuid().ToString()),
                iterations: 10,
                maxTotal: 300.Milliseconds()
            );
        }

        [Test]
        public void Repeated_fetching_of_same_connection_runs_200_times_in_ten_milliseconds()
        {
            var dbName = "4669B59A-E0AC-4E76-891C-7A2369AE0F2F";
            using var manager = new SqlServerDatabasePool();
            manager.SetLogLevel(LogLevel.Warning);
            manager.ConnectionStringFor(dbName);

            TimeAsserter.Execute(
                action: () => manager.ConnectionStringFor(dbName),
                iterations: 200,
                maxTotal: 10.Milliseconds()
            );
        }

        [Test] public void Once_DB_Fetched_Can_use_400_connections_in_10_milliseconds()
        {
            using var manager = new SqlServerDatabasePool();
            manager.SetLogLevel(LogLevel.Warning);
            var connectionProvider = new SqlServerConnectionProvider(manager.ConnectionStringFor("4669B59A-E0AC-4E76-891C-7A2369AE0F2F"));
            connectionProvider.UseConnection(_ => { });

            TimeAsserter.Execute(
                action: () => connectionProvider.UseConnection(_ => { }),
                iterations: 400,
                maxTotal: 10.Milliseconds()
            );
        }
    }
}
