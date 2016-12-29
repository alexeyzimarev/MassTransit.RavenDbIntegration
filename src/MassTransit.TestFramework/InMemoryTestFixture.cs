﻿using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit.Logging;
using MassTransit.Pipeline.Pipes;
using MassTransit.Transports.InMemory;
using MassTransit.Util;
using NUnit.Framework;

namespace MassTransit.TestFramework
{
    // Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
    //
    // Licensed under the Apache License, Version 2.0 (the "License"); you may not use
    // this file except in compliance with the License. You may obtain a copy of the
    // License at
    //
    //     http://www.apache.org/licenses/LICENSE-2.0
    //
    // Unless required by applicable law or agreed to in writing, software distributed
    // under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
    // CONDITIONS OF ANY KIND, either express or implied. See the License for the
    // specific language governing permissions and limitations under the License.

    public class InMemoryTestFixture :
        BusTestFixture
    {
        static readonly ILog _log = Logger.Get<InMemoryTestFixture>();

        IBusControl _bus;
        Uri _inputQueueAddress;
        ISendEndpoint _inputQueueSendEndpoint;
        ISendEndpoint _busSendEndpoint;
        BusHandle _busHandle;
        readonly Uri _baseAddress;
        InMemoryHost _inMemoryTransportCache;
        readonly IBusCreationScope _busCreationScope;

        public Uri BaseAddress => _baseAddress;

        public InMemoryTestFixture(bool busPerTest = false)
        {
            _baseAddress = new Uri("loopback://localhost/");

            _inputQueueAddress = new Uri("loopback://localhost/input_queue");

            if (busPerTest)
                _busCreationScope = new PerTestBusCreationScope(SetupBus, TeardownBus);
            else
                _busCreationScope = new PerTestFixtureBusCreationScope(SetupBus, TeardownBus);
        }

        /// <summary>
        /// The sending endpoint for the InputQueue
        /// </summary>
        protected ISendEndpoint InputQueueSendEndpoint => _inputQueueSendEndpoint;

        /// <summary>
        /// The sending endpoint for the Bus
        /// </summary>
        protected ISendEndpoint BusSendEndpoint => _busSendEndpoint;

        protected Uri BusAddress => _bus.Address;

        protected Uri InputQueueAddress
        {
            get { return _inputQueueAddress; }
            set
            {
                if (Bus != null)
                    throw new InvalidOperationException("The LocalBus has already been created, too late to change the URI");

                _inputQueueAddress = value;
            }
        }

        protected override IBus Bus => _bus;

        protected IRequestClient<TRequest, TResponse> CreateRequestClient<TRequest, TResponse>()
            where TRequest : class
            where TResponse : class
        {
            return Bus.CreateRequestClient<TRequest, TResponse>(InputQueueAddress, TestTimeout);
        }

        [TestFixtureSetUp]
        public void SetupInMemoryTestFixture()
        {
            _busCreationScope.TestFixtureSetup();
        }

        [SetUp]
        public void SetupInMemoryTest()
        {
            _busCreationScope.TestSetup();
        }

        void SetupBus()
        {
            _bus = CreateBus();

            ConnectObservers(_bus);

            _busHandle = Await(async () => await _bus.StartAsync());

            _busSendEndpoint = Await(() => GetSendEndpoint(_bus.Address));

            _inputQueueSendEndpoint = Await(() => GetSendEndpoint(InputQueueAddress));
        }

        protected async Task<ISendEndpoint> GetSendEndpoint(Uri address)
        {
            ISendEndpoint sendEndpoint = await _bus.GetSendEndpoint(address).ConfigureAwait(false);

            return sendEndpoint;
        }

        protected IPublishEndpointProvider PublishEndpointProvider => new InMemoryPublishEndpointProvider(Bus, _inMemoryTransportCache, PublishPipe.Empty);

        [TestFixtureTearDown]
        public void TearDownInMemoryTestFixture()
        {
            _busCreationScope.TestFixtureTeardown();
        }

        [TearDown]
        public void TearDownInMemoryTest()
        {
            _busCreationScope.TestTeardown();
        }

        void TeardownBus()
        {
            try
            {
                Await(async () => await _busHandle?.StopAsync(new CancellationTokenSource(TestTimeout).Token));
            }
            catch (Exception ex)
            {
                _log.Error("Bus Stop Failed: ", ex);
                throw;
            }
            finally
            {
                _busHandle = null;
                _bus = null;
            }
        }

        protected virtual void ConfigureBus(IInMemoryBusFactoryConfigurator configurator)
        {
        }

        protected virtual void ConnectObservers(IBus bus)
        {

        }

        protected virtual void ConfigureInputQueueEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
        {
        }

        IBusControl CreateBus()
        {
            return MassTransit.Bus.Factory.CreateUsingInMemory(x =>
            {
                _inMemoryTransportCache = new InMemoryHost(Environment.ProcessorCount);

                x.SetHost(_inMemoryTransportCache);
                ConfigureBus(x);

                x.ReceiveEndpoint("input_queue", configurator => ConfigureInputQueueEndpoint(configurator));
            });
        }



        interface IBusCreationScope
        {
            void TestFixtureSetup();
            void TestSetup();
            void TestTeardown();
            void TestFixtureTeardown();

        }



        class PerTestFixtureBusCreationScope : IBusCreationScope
        {
            readonly Action _setupBus;
            readonly Action _teardownBus;

            public PerTestFixtureBusCreationScope(Action setupBus, Action teardownBus)
            {
                _setupBus = setupBus;
                _teardownBus = teardownBus;
            }

            public void TestFixtureSetup()
            {
                _setupBus();
            }

            public void TestSetup()
            {
            }

            public void TestTeardown()
            {
            }

            public void TestFixtureTeardown()
            {
                _teardownBus();
            }
        }
        class PerTestBusCreationScope : IBusCreationScope
        {
            readonly Action _setupBus;
            readonly Action _teardownBus;

            public PerTestBusCreationScope(Action setupBus, Action teardownBus)
            {
                _setupBus = setupBus;
                _teardownBus = teardownBus;
            }

            public void TestFixtureSetup()
            {
            }

            public void TestSetup()
            {
                _setupBus();
            }

            public void TestTeardown()
            {
                _teardownBus();
            }

            public void TestFixtureTeardown()
            {
            }
        }
    }
}