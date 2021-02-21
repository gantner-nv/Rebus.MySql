﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.MySql.Transport;
using Rebus.Tests.Contracts;
using Rebus.Threading;
using Rebus.Time;

namespace Rebus.MySql.Tests.Integration
{
    [TestFixture]
    public class TestNumberOfSqlConnections : FixtureBase
    {
        [Test]
        public async Task CountTheConnections()
        {
            var activeConnections = new ConcurrentDictionary<int, object>();

            var bus = Configure.With(new BuiltinHandlerActivator())
                .Transport(t => t.Register(c =>
                {
                    var connectionProvider = new TestConnectionProvider(MySqlTestHelper.ConnectionString, activeConnections);
                    var transport = new MySqlTransport(connectionProvider, "bimse", c.Get<IRebusLoggerFactory>(), c.Get<IAsyncTaskFactory>(), c.Get<IRebusTime>(), new MySqlTransportOptions(connectionProvider));

                    transport.EnsureTableIsCreated();

                    return transport;
                }))
                .Start();

            using (new Timer(_ => Console.WriteLine("Active connections: {0}", activeConnections.Count), null, 0, 1000))
            {
                using (bus)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }

        class TestConnectionProvider : IDbConnectionProvider
        {
            static int _counter;

            readonly ConcurrentDictionary<int, object> _activeConnections;
            readonly IDbConnectionProvider _inner;

            public TestConnectionProvider(string connectionString, ConcurrentDictionary<int, object> activeConnections)
            {
                _activeConnections = activeConnections;
                _inner = new DbConnectionProvider(connectionString, new ConsoleLoggerFactory(true));
            }

            public IDbConnection GetConnection()
            {
                return new Bimse(_inner.GetConnection(), Interlocked.Increment(ref _counter), _activeConnections);
            }

            public async Task<IDbConnection> GetConnectionAsync()
            {
                return new Bimse(await _inner.GetConnectionAsync(), Interlocked.Increment(ref _counter), _activeConnections);
            }

            class Bimse : IDbConnection
            {
                readonly IDbConnection _innerConnection;
                readonly ConcurrentDictionary<int, object> _activeConnections;
                readonly int _id;

                public Bimse(IDbConnection innerConnection, int id, ConcurrentDictionary<int, object> activeConnections)
                {
                    _innerConnection = innerConnection;
                    _id = id;
                    _activeConnections = activeConnections;
                    _activeConnections[id] = new object();
                }

                public string Database => _innerConnection.Database;

                public MySqlCommand CreateCommand()
                {
                    return _innerConnection.CreateCommand();
                }

                public IEnumerable<TableName> GetTableNames()
                {
                    return _innerConnection.GetTableNames();
                }

                public void Complete()
                {
                    _innerConnection.Complete();
                }

                public async Task CompleteAsync()
                {
                    await _innerConnection.CompleteAsync();
                }

                public Dictionary<string, string> GetColumns(string schema, string dataTableName)
                {
                    return _innerConnection.GetColumns(schema, dataTableName);
                }

                public Dictionary<string, string> GetIndexes(string schema, string dataTableName)
                {
                    return _innerConnection.GetIndexes(schema, dataTableName);
                }

                public void ExecuteCommands(string sqlCommands)
                {
                    _innerConnection.ExecuteCommands(sqlCommands);
                }

                public void Dispose()
                {
                    _innerConnection.Dispose();
                    _activeConnections.TryRemove(_id, out _);
                }
            }
        }
    }
}
