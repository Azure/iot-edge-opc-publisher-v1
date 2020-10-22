// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using OpcPublisher.Configurations;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OpcPublisher
{
    [Collection("Need PLC and publisher config")]
    public sealed class ConfigurationViaIotMethodUnitTests
    {
        public ConfigurationViaIotMethodUnitTests(ITestOutputHelper output, PlcOpcUaServerFixture server)
        {
            // xunit output
            _output = output;
            _server = server;
        }

        private void CheckWhetherToSkip() {
            Skip.If(_server.Plc == null, "Server not reachable - Ensure docker endpoint is properly configured.");
        }

        /// <summary>
        /// Test that OpcPublishingInterval setting is not persisted when not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadPublishingIntervalUnset))]
        public async Task OpcPublishingIntervalUnsetAndIsNotPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == SettingsConfiguration.DefaultOpcPublishingInterval);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == null);
    }

        /// <summary>
        /// Test that OpcPublishingInterval setting is persisted when configured via method and is different as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadPublishingInterval2000))]
        public async Task OpcPublishingInterval2000AndDifferentAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 3000;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
        }

        /// <summary>
        /// Test that OpcPublishingInterval setting is persisted when configured via method and is same as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadPublishingInterval2000))]
        public async Task OpcPublishingInterval2000AndSameAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcpublishinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
        }

        /// <summary>
        /// Test that OpcSamplingInterval setting is not persisted when not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSamplingIntervalUnset))]
        public async Task OpcSamplingIntervalUnsetAndIsNotPersisted(string testFilename, string payloadFilename) {
            CheckWhetherToSkip();

            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename)) {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == SettingsConfiguration.DefaultOpcSamplingInterval);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == null);
        }

        /// <summary>
        /// Test that OpcSamplingInterval setting is persisted when configured via method and is different as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSamplingInterval2000))]
        public async Task OpcSamplingInterval2000AndDifferentAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();
 
            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
        }

        /// <summary>
        /// Test that OpcSamplingInterval setting is persisted when configured via method and is same as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSamplingInterval2000))]
        public async Task OpcSamplingInterval2000AndSameAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/opcsamplinginterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 2000;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();
 
            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
        }

        /// <summary>
        /// Test that SkipFirst setting is not persisted when default is false and not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstUnset))]
        public async Task SkipFirstUnsetDefaultFalseAndIsNotPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == false);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == null);
        }

        /// <summary>
        /// Test that SkipFirst setting is not persisted when default is true and not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstUnset))]
        public async Task SkipFirstUnsetDefaultTrueAndIsNotPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == true);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == null);
        }

        /// <summary>
        /// Test that skipFirst setting is persisted when configured true via method and is different as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstTrue))]
        public async Task SkipFirstTrueAndDifferentAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == true);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
        }

        /// <summary>
        /// Test that skipFirst setting is persisted when configured true via method and is save as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstTrue))]
        public async Task SkipFirstTrueAndSameAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();
 
            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == true);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
        }

        /// <summary>
        /// Test that skipFirst setting is persisted when configured false via method and is different as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstFalse))]
        public async Task SkipFirstFalseAndDifferentAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == false);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
        }

        /// <summary>
        /// Test that skipFirst setting is persisted when configured false via method and is same as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcEmptyAndPayloadSkipFirstFalse))]
        public async Task SkipFirstFalseAndSameAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == false);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is not persisted when default is 0 not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadHeartbeatIntervalUnset))]
        public async Task HeartbeatIntervalUnsetDefaultZeroAndIsNotPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 0;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();
 
            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 0);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == null);
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is not persisted when default is 2 and not configured via method.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadHeartbeatIntervalUnset))]
        public async Task HeartbeatIntervalUnsetDefaultNotZeroAndIsNotPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 2;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == null);
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is persisted when configured 2 via method and is different as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadHeartbeatInterval2))]
        public async Task HeartbeatInterval2AndDifferentAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 1;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();

            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is persisted when configured 2 via method and is save as default.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "DirectMethod")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcEmptyAndPayloadHeartbeatInterval2))]
        public async Task HeartbeatInterval2AndSameAsDefaultAndIsPersisted(string testFilename, string payloadFilename)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqPayloadFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{payloadFilename}";
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/heartbeatinterval/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();
            HubClientWrapper clientWrapper = new HubClientWrapper();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 2;

            nodeConfig.Init();
            clientWrapper.InitMessageProcessing();
 
            Assert.True(nodeConfig.OpcSessions.Count == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 0, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 0, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await clientWrapper._hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(nodeConfig.OpcSessions.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers.Count == 1);
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(nodeConfig.OpcSessions.Count == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == 1, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == 1, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
        }

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadPublishingInterval2000 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_publishinginterval_2000.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadPublishingIntervalUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_publishinginterval_unset.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadSamplingInterval2000 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_samplinginterval_2000.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadSamplingIntervalUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_samplinginterval_unset.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadSkipFirstUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_skipfirst_unset.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadSkipFirstTrue =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_skipfirst_true.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadSkipFirstFalse =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_skipfirst_false.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadHeartbeatIntervalUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_heartbeatinterval_unset.json"),
                },
            };

        public static IEnumerable<object[]> PnPlcEmptyAndPayloadHeartbeatInterval2 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_empty.json"),
                    // method payload file
                    new string($"pn_plc_request_payload_heartbeatinterval_2.json"),
                },
            };

        private readonly ITestOutputHelper _output;
        private readonly PlcOpcUaServerFixture _server;
        private static List<ConfigurationFileEntryLegacyModel> _configurationFileEntries;
    }
}
