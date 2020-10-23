// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Moq;
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
    public sealed class ConfigurationViaFileUnitTests
    {
        public ConfigurationViaFileUnitTests(ITestOutputHelper output, PlcOpcUaServerFixture server)
        {
            // xunit output
            _output = output;
            _server = server;
        }

        private void CheckWhetherToSkip() {
            Skip.If(_server.Plc == null, "Server not reachable - Ensure docker endpoint is properly configured.");
        }

        /// <summary>
        /// Test reading different configuration files and creating the correct internal data structures.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "NodeIdSyntax")]
        [MemberData(nameof(PnPlcSimple))]
        public void CreateOpcPublishingData(string testFilename, int configuredSessions,
            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/publishernodeconfiguration/{testFilename}";
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
            
            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that when no OpcPublishingInterval setting configured, it is not persisted.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcOpcPublishingIntervalNone))]
        public async Task OpcPublishingIntervalUnsetAndNotPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == SettingsConfiguration.DefaultOpcPublishingInterval);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == null);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that OpcPublishingInterval setting is kept when different as default setting.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcOpcPublishingInterval2000))]
        public async Task OpcPublishingInterval2000DifferentThanDefaultAndIsPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 3000;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that OpcPublishingInterval setting is not removed when default setting is the same.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcPublishingInterval")]
        [MemberData(nameof(PnPlcOpcPublishingInterval2000))]
        public async Task OpcPublishingInterval2000SameAsDefaultAndIsPersisted(string testFilename, int configuredSessions,
                                    int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].PublishingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that when no OpcSamplingInterval setting configured, it is not persisted.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcOpcSamplingIntervalNone))]
        public async Task OpcSamplingIntervalUnsetAndNotPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 3000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == null);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that OpcSamplingInterval setting is kept when different as default setting.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcOpcSamplingInterval2000))]
        public async Task OpcSamplingInterval2000DifferentThanDefaultAndIsPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that OpcSamplingInterval setting is not removed when default setting is the same.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "OpcSamplingInterval")]
        [MemberData(nameof(PnPlcOpcSamplingInterval2000))]
        public async Task OpcSamplingInterval2000SameAsDefaultAndIsPersisted(string testFilename, int configuredSessions,
                                    int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.DefaultOpcSamplingInterval = 2000;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that when no SkipFirst setting configured, it is not persisted.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcSkipFirstUnset))]
        public async Task SkipFirstUnsetAndNotPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
            string fqTestFilename = $"{Directory.GetCurrentDirectory()}/testdata/skipfirst/{testFilename}";
            fqTempFilename = $"{Directory.GetCurrentDirectory()}/tempdata/{methodName}_{testFilename}";
            if (File.Exists(fqTempFilename))
            {
                File.Delete(fqTempFilename);
            }
            File.Copy(fqTestFilename, fqTempFilename);
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            PublishedNodesConfiguration nodeConfig = new PublishedNodesConfiguration();

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == SettingsConfiguration.SkipFirstDefault);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == null);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that SkipFirst setting is kept when different as default setting.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcSkipFirstTrue))]
        public async Task SkipfirstTrueIsDifferentThanDefaultAndIsPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.SkipFirstDefault = false;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == true);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that SkipFirst setting is kept when different as default setting.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcSkipFirstFalse))]
        public async Task SkipfirstFalseIsDifferentThanDefaultAndIsPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.SkipFirstDefault = true;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == false);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that SkipFirst setting is not removed when default setting is the same.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcSkipFirstFalse))]
        public async Task SkipFirstFalseIsSameAsDefaultAndIsPersisted(string testFilename, int configuredSessions,
                                    int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.SkipFirstDefault = false;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == false);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that SkipFirst setting is not removed when default setting is the same.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "SkipFirst")]
        [MemberData(nameof(PnPlcSkipFirstTrue))]
        public async Task SkipFirstTrueSameAsDefaultAndIsPersisted(string testFilename, int configuredSessions,
                                    int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.SkipFirstDefault = true;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].SkipFirst == true);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            nodeConfig.Close();
        }


        /// <summary>
        /// Test that when no HeartbeatInterval setting configured, it is not persisted.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcHeartbeatIntervalUnset))]
        public async Task HeartbeatIntervalUnsetAndNotPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == SettingsConfiguration.HeartbeatIntervalDefault);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == null);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is kept when different as default setting.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcHeartbeatInterval2))]
        public async Task HeartbeatInterval2IsDifferentThanDefaultAndIsPersisted(string testFilename, int configuredSessions,
                            int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.HeartbeatIntervalDefault = 5;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            nodeConfig.Close();
        }

        /// <summary>
        /// Test that HeartbeatInterval setting is not removed when default setting is the same.
        /// </summary>
        [SkippableTheory]
        [Trait("Configuration", "File")]
        [Trait("ConfigurationSetting", "HeartbeatInterval")]
        [MemberData(nameof(PnPlcHeartbeatInterval2))]
        public async Task HeartbeatInterval2IsSameAsDefaultAndIsPersisted(string testFilename, int configuredSessions,
                                    int configuredSubscriptions, int configuredMonitoredItems)
        {
            CheckWhetherToSkip();
            string methodName = UnitTestHelper.GetMethodName();
            string fqTempFilename = string.Empty;
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            nodeConfig.Init();
            SettingsConfiguration.HeartbeatIntervalDefault = 2;
            Assert.True(nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
            Assert.True(nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {nodeConfig.NumberOfOpcSessionsConfigured}, connected {nodeConfig.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {nodeConfig.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            Assert.True(nodeConfig.OpcSessions[0].OpcSubscriptionWrappers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
            await nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            nodeConfig.Close();
        }

        public static IEnumerable<object[]> PnPlcSimple =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_nid.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    2
                },
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_nid_enid.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    3
                },
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_nid_enid_id.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    6
                },
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_enid.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_enid_id.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    4
                },
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_simple_id.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    3
                },
            };

        public static IEnumerable<object[]> PnPlcOpcPublishingIntervalNone =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_publishinginterval_unset.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcOpcPublishingInterval2000 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_publishinginterval_2000.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcOpcSamplingIntervalNone =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_samplinginterval_unset.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcOpcSamplingInterval2000 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_samplinginterval_2000.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcSkipFirstUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_skipfirst_unset.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcSkipFirstFalse =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_skipfirst_false.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcSkipFirstTrue =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_skipfirst_true.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcHeartbeatIntervalUnset =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_heartbeatinterval_unset.json"),
                    // # of configured sessions
                    1,
                    // # of configured subscriptions
                    1,
                    // # of configured monitored items
                    1
                },
            };

        public static IEnumerable<object[]> PnPlcHeartbeatInterval2 =>
            new List<object[]>
            {
                new object[] {
                    // published nodes configuration file
                    new string($"pn_plc_heartbeatinterval_2.json"),
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
        private static List<ConfigurationFileEntryLegacyModel> _configurationFileEntries;
    }
}
