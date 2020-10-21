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

            UnitTestHelper.SetPublisherDefaults();

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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
            var nodeConfigurationMockBase = new Mock<PublisherNodeConfiguration>();
            var nodeConfigurationMock = nodeConfigurationMockBase.As<PublisherNodeConfiguration>();
            nodeConfigurationMock.CallBase = true;

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcPublishingInterval = 2000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].RequestedPublishingInterval == OpcApplicationConfiguration.OpcPublishingInterval);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == null);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcPublishingInterval = 3000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].RequestedPublishingInterval == 2000);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcPublishingInterval = 2000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].RequestedPublishingInterval == 2000);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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
            var nodeConfigurationMockBase = new Mock<PublisherNodeConfiguration>();
            var nodeConfigurationMock = nodeConfigurationMockBase.As<PublisherNodeConfiguration>();
            nodeConfigurationMock.CallBase = true;

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcSamplingInterval = 3000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 3000);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == null);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcSamplingInterval = 3000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcApplicationConfiguration.OpcSamplingInterval = 2000;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].RequestedSamplingInterval == 2000);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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
            OpcUaMonitoredItemManager.SkipFirstDefault = false;
            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));
            var nodeConfigurationMockBase = new Mock<PublisherNodeConfiguration>();
            var nodeConfigurationMock = nodeConfigurationMockBase.As<PublisherNodeConfiguration>();
            nodeConfigurationMock.CallBase = true;

            UnitTestHelper.SetPublisherDefaults();

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].SkipFirst == OpcUaMonitoredItemManager.SkipFirstDefault);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == null);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.SkipFirstDefault = false;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].SkipFirst == true);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.SkipFirstDefault = true;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].SkipFirst == false);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.SkipFirstDefault = false;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].SkipFirst == false);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.SkipFirstDefault = true;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].SkipFirst == true);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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
            var nodeConfigurationMockBase = new Mock<PublisherNodeConfiguration>();
            var nodeConfigurationMock = nodeConfigurationMockBase.As<PublisherNodeConfiguration>();
            nodeConfigurationMock.CallBase = true;

            UnitTestHelper.SetPublisherDefaults();

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].HeartbeatInterval == OpcUaMonitoredItemManager.HeartbeatIntervalDefault);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == null);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.HeartbeatIntervalDefault = 5;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
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

            UnitTestHelper.SetPublisherDefaults();
            OpcUaMonitoredItemManager.HeartbeatIntervalDefault = 2;

            try
            {
                Assert.True(Program.Instance._nodeConfig.OpcSessions.Count == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured == configuredSessions, "wrong # of sessions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured == configuredSubscriptions, "wrong # of subscriptions");
                Assert.True(Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured == configuredMonitoredItems, "wrong # of monitored items");
                _output.WriteLine($"sessions configured {Program.Instance._nodeConfig.NumberOfOpcSessionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSessionsConnected}");
                _output.WriteLine($"subscriptions configured {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConfigured}, connected {Program.Instance._nodeConfig.NumberOfOpcSubscriptionsConnected}");
                _output.WriteLine($"items configured {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsConfigured}, monitored {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsMonitored}, toRemove {Program.Instance._nodeConfig.NumberOfOpcMonitoredItemsToRemove}");
                Assert.True(Program.Instance._nodeConfig.OpcSessions[0].OpcSubscriptionManagers[0].OpcMonitoredItems[0].HeartbeatInterval == 2);
                await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
                _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
                Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            }
            finally
            {
                Program.Instance._nodeConfig = null;
            }
            Program.Instance._nodeConfig = null;
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
