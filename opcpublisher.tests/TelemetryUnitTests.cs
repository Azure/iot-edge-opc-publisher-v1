// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using Moq;
using OpcPublisher.Configurations;
using OpcPublisher.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OpcPublisher
{
    [Collection("Need PLC and publisher config")]
    public sealed class TelemetryUnitTests : IDisposable
    {
        public TelemetryUnitTests(ITestOutputHelper output, PlcOpcUaServerFixture server)
        {
            // xunit output
            _output = output;
            _server = server;

            // init static publisher objects
            Program.TelemetryConfiguration = PublisherTelemetryConfiguration.Instance;
            Program.Diag = PublisherDiagnostics.Instance;
        }

        private void CheckWhetherToSkip() {
            Skip.If(_server.Plc == null, "Server not reachable - Ensure docker endpoint is properly configured.");
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            // do cleanup
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Test telemetry is sent to the hub.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "Basic")]
        [MemberData(nameof(PnPlcCurrentTime))]
        public async Task TelemetryIsSentAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            UnitTestHelper.SetPublisherDefaults();

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;

            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(2500).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.Equal(3, eventsAfterDelay - eventsAtStart);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        /// <summary>
        /// Test telemetry is sent to the hub using node with static value.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "Basic")]
        [MemberData(nameof(PnPlcProductName))]
        public async Task TelemetryIsSentWithStaticNodeValueAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            UnitTestHelper.SetPublisherDefaults();

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;

            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(3000).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.Equal(1, eventsAfterDelay - eventsAtStart);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        /// <summary>
        /// Test first event is skipped.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "SkipFirst")]
        [MemberData(nameof(PnPlcCurrentTime))]
        public async Task FirstTelemetryEventIsSkippedAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.SkipFirstDefault = true;

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;

            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(1900).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.True(eventsAfterDelay - eventsAtStart == 1);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        /// <summary>
        /// Test first event is skipped using a node with static value.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "SkipFirst")]
        [MemberData(nameof(PnPlcProductName))]
        public async Task FirstTelemetryEventIsSkippedWithStaticNodeValueAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            OpcUaMonitoredItemManager.HeartbeatIntervalDefault = 0;
            OpcUaMonitoredItemManager.SkipFirstDefault = true;

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;
            
            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(3000).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.True(eventsAfterDelay - eventsAtStart == 0);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        /// <summary>
        /// Test heartbeat is working on a node with static value.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "Heartbeat")]
        [MemberData(nameof(PnPlcProductNameHeartbeat2))]
        public async Task HeartbeatOnStaticNodeValueIsWorkingAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            OpcUaMonitoredItemManager.HeartbeatIntervalDefault = 0;

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;
            
            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(5000).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.Equal(2, eventsAfterDelay - eventsAtStart);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        /// <summary>
        /// Test heartbeat is working on a node with static value with skip first true.
        /// </summary>
        [SkippableTheory]
        [Trait("Telemetry", "All")]
        [Trait("TelemetryFunction", "Heartbeat")]
        [MemberData(nameof(PnPlcProductNameHeartbeat2SkipFirst))]
        public async Task HeartbeatWithSkipFirstOnStaticNodeValueIsWorkingAsync(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/telemetry/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            PublisherNodeConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {PublisherNodeConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(PublisherNodeConfiguration.PublisherNodeConfigurationFilename));

            OpcUaMonitoredItemManager.HeartbeatIntervalDefault = 0;

            // mock IoTHub communication
            var hubMockBase = new Mock<HubMethodHandler>();
            var hubMock = hubMockBase.As<IHubMethodHandler>();
            hubMock.CallBase = true;

            // configure hub client mock
            var hubClientMockBase = new Mock<HubClientWrapper>();
            var hubClientMock = hubClientMockBase.As<IHubClientWrapper>();
            hubClientMock.CallBase = true;

            int eventsReceived = 0;
            hubClientMock.Setup(m => m.SendEvent(It.IsAny<Message>())).Callback<Message>(m => eventsReceived++);
            hubClientMock.Object.InitMessageProcessing();

            try
            {
                long eventsAtStart = hubClientMockBase.Object.NumberOfEvents;
                Program.NodeConfiguration = PublisherNodeConfiguration.Instance;
                Assert.True(Program.NodeConfiguration.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived();
                long eventsAfterConnect = hubClientMockBase.Object.NumberOfEvents;
                await Task.Delay(3000).ConfigureAwait(false);
                long eventsAfterDelay = hubClientMockBase.Object.NumberOfEvents;
                _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
                _output.WriteLine($"sessions configured {Program.NodeConfiguration.NumberOfOpcSessionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.NodeConfiguration.NumberOfOpcSubscriptionsConfigured}, connected {Program.NodeConfiguration.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.NodeConfiguration.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.NodeConfiguration.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.NodeConfiguration.NumberOfOpcMonitoredItemsToRemove}");
                _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
                hubClientMock.VerifySet(m => m.ProductInfo = "OpcPublisher");
                Assert.True(eventsAfterDelay - eventsAtStart == 2);
            }
            finally
            {
                Program.NodeConfiguration = null;
                hubClientMockBase.Object.Close();
            }
        }

        public static IEnumerable<object[]> PnPlcCurrentTime =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_currenttime.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcProductName =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_productname.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcProductNameHeartbeat2 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_productname_heartbeatinterval_2.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcProductNameHeartbeat2SkipFirst =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_productname_heartbeatinterval_2_skipfirst.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        private readonly ITestOutputHelper _output;
        private readonly PlcOpcUaServerFixture _server;
    }
}
