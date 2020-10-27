// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Configuration;
using OpcPublisher.Configurations;
using System;
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 3000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 2000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = true;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.SkipFirstDefault = false;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 0;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 2;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            await Task.Yield();
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 1;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Configurations/Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(OpcPublisherFixture.CertificateValidator_CertificateValidation);

            _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.HeartbeatIntervalDefault = 2;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == 0, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 0, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 0, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries.Count == 0);
            MethodRequest methodRequest = new MethodRequest("PublishNodes", File.ReadAllBytes(fqPayloadFilename));
            await hubMethodHandler.HandlePublishNodesMethodAsync(methodRequest, null).ConfigureAwait(false);
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1);
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1);
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(Metrics.NumberOfOpcSessionsConnected == 1, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == 1, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == 1, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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
        private List<ConfigurationFileEntryLegacyModel> _configurationFileEntries;
        private ApplicationInstance _application;
        private UAClient _uaClient;
    }
}
