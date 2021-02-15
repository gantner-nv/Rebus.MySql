﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Logging;
using Rebus.MySql.Sagas;
using Rebus.MySql.Sagas.Serialization;
using Rebus.Sagas;
using Rebus.Tests.Contracts;

namespace Rebus.MySql.Tests.Sagas
{
    [TestFixture]
    public class TestMySqlSagaStoragePerformance : FixtureBase
    {
        MySqlSagaStorage _storage;

        protected override void SetUp()
        {
            var loggerFactory = new ConsoleLoggerFactory(false);
            var connectionProvider = new DbConnectionProvider(MySqlTestHelper.ConnectionString, loggerFactory);
            var sagaTypeNamingStrategy = new LegacySagaTypeNamingStrategy();
            var serializer = new DefaultSagaSerializer();

            var dataTableName = TestConfig.GetName("sagas");
            var indexTableName = TestConfig.GetName("sagaindex");

            MySqlTestHelper.DropTable(indexTableName);
            MySqlTestHelper.DropTable(dataTableName);

            _storage = new MySqlSagaStorage(connectionProvider, dataTableName, indexTableName, loggerFactory, sagaTypeNamingStrategy, serializer);

            _storage.EnsureTablesAreCreated();
        }

        [Test]
        public async Task TimeToInsertBigSaga()
        {
            var sagaData = GetSagaData();

            var elapsed = await TakeTime(async () =>
            {
                await _storage.Insert(sagaData, Enumerable.Empty<ISagaCorrelationProperty>());
            });

            Console.WriteLine($"Inserting saga data with {sagaData.BigString.Length} chars took {elapsed.TotalSeconds:0.0} s");
        }

        [Test]
        public async Task TimeToLoadBigSaga()
        {
            var sagaData = GetSagaData();

            await _storage.Insert(sagaData, Enumerable.Empty<ISagaCorrelationProperty>());

            var elapsed = await TakeTime(async () =>
            {
                var loadedData = await _storage.Find(typeof(BigStringSagaData), "Id", sagaData.Id.ToString());

                Console.WriteLine(loadedData.Id.ToString());
            });

            Console.WriteLine($"Loading saga data with {sagaData.BigString.Length} chars took {elapsed.TotalSeconds:0.00} s");
        }

        [Test]
        public async Task TimeToUpdateBigSaga()
        {
            var sagaData = GetSagaData();

            await _storage.Insert(sagaData, Enumerable.Empty<ISagaCorrelationProperty>());

            var elapsed = await TakeTime(async () =>
            {
                await _storage.Update(sagaData, Enumerable.Empty<ISagaCorrelationProperty>());
            });

            Console.WriteLine($"Updating saga data with {sagaData.BigString.Length} chars took {elapsed.TotalSeconds:0.0} s");
        }

        async Task<TimeSpan> TakeTime(Func<Task> asyncAction)
        {
            var stopwatch = Stopwatch.StartNew();
            await asyncAction();
            return stopwatch.Elapsed;
        }

        static BigStringSagaData GetSagaData()
        {
            return new BigStringSagaData
            {
                Id = Guid.NewGuid(),
                Revision = 0,
                BigString = string.Join(Environment.NewLine, Enumerable.Repeat("this is just a line of text", 100000))
            };
        }

        class BigStringSagaData : ISagaData
        {
            public Guid Id { get; set; }
            public int Revision { get; set; }
            public string BigString { get; set; }
        }
    }
}
