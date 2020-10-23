// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using OpcPublisher.Configurations;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OpcPublisher
{
    [Collection("Need PLC and publisher config")]
    public sealed class TelemetryUnitTests
    {
        public TelemetryUnitTests(ITestOutputHelper output, PlcOpcUaServerFixture server)
        {
            // xunit output
            _output = output;
            _server = server;
        }

        private void CheckWhetherToSkip() {
            Skip.If(_server.Plc == null, "Server not reachable - Ensure docker endpoint is properly configured.");
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            nodeConfig.Init();
            hubClient.InitMessageProcessing();

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));
                        
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(2, eventsAfterDelay - eventsAfterConnect);
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started");
            nodeConfig.Close();
            hubClient.Close();
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            hubClient.InitMessageProcessing();
            
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(1, eventsAfterDelay - eventsAfterConnect);
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            nodeConfig.Close();
            hubClient.Close();
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            hubClient.InitMessageProcessing();

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(3, eventsAfterDelay - eventsAfterConnect); 
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            nodeConfig.Close();
            hubClient.Close();
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            hubClient.InitMessageProcessing();

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(1, eventsAfterDelay - eventsAfterConnect);
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            nodeConfig.Close();
            hubClient.Close();
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            hubClient.InitMessageProcessing();

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(3, eventsAfterDelay - eventsAfterConnect); 
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            nodeConfig.Close();
            hubClient.Close();
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

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper hubClient = new HubClientWrapper();

            //TODO: Temporary workaround until the Program.Instance dependencies can be removed
            Program.Instance._nodeConfig = nodeConfig;

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            hubClient.InitMessageProcessing();

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = PublisherDiagnostics.NumberOfEvents;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived(nodeConfig);
            long eventsAfterConnect = PublisherDiagnostics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = PublisherDiagnostics.NumberOfEvents;
            Assert.Equal(1, eventsAfterDelay - eventsAfterConnect);
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            nodeConfig.Close();
            hubClient.Close();
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
