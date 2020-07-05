using System;
using Composable.DependencyInjection;
using Composable.Persistence.SqlServer.SystemExtensions;
using Composable.Persistence.SqlServer.Testing.Databases;
using Composable.System.Configuration;
using FluentAssertions;
using NCrunch.Framework;
using NUnit.Framework;

namespace Composable.Tests.ExternalDependencies.SqlServerDatabasePoolTests
{
    [TestFixture] public class After_creating_two_databases_named_db1_and_db2
    {
        SqlServerDatabasePool _manager;
        ISqlServerConnectionProvider _dB1ConnectionProvider;
        ISqlServerConnectionProvider _dB2ConnectionProvider;
        const string Db1 = "LocalDBManagerTests_After_creating_connection_Db1";
        const string Db2 = "LocalDBManagerTests_After_creating_connection_Db2";

        [SetUp] public void SetupTask()
        {
            _manager = new SqlServerDatabasePool();
            _dB1ConnectionProvider = ConnectionProviderFor(Db1);
            _dB2ConnectionProvider = ConnectionProviderFor(Db2);
        }

        [Test] public void Connection_to_Db1_can_be_opened_and_used()
            => _dB1ConnectionProvider.ExecuteScalar(commandText: "select 1").Should().Be(expected: 1);

        [Test] public void Connection_to_Db2_can_be_opened_and_used()
            => _dB2ConnectionProvider.ExecuteScalar(commandText: "select 1").Should().Be(expected: 1);

        [Test] public void The_same_connection_string_is_returned_by_each_call_to_CreateOrGetLocalDb_Db1()
            => _manager.ConnectionStringFor(Db1).Should().Be(_manager.ConnectionStringFor(Db1));

        [Test] public void The_same_connection_string_is_returned_by_each_call_to_CreateOrGetLocalDb_Db2()
            => _manager.ConnectionStringFor(Db2).Should().Be(_manager.ConnectionStringFor(Db2));

        [Test] public void The_Db1_connectionstring_is_different_from_the_Db2_connection_string()
            => _manager.ConnectionStringFor(Db1).Should().NotBe(_manager.ConnectionStringFor(Db2));

        [TearDown] public void TearDownTask()
        {
            _manager.Dispose();

            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            _manager.Invoking(action: man => ConnectionProviderFor(Db1))
                    .Should().Throw<Exception>()
                    .Where(exceptionExpression: exception => exception.Message.ToLower()
                                                                      .Contains("disposed"));
        }

        ISqlServerConnectionProvider ConnectionProviderFor(string connectionName) => new SqlServerConnectionProvider(_manager.ConnectionStringFor(connectionName));
    }
}
