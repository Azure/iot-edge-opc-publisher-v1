// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Moq;
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
            
            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            // wait 5 seconds for the server to become available
            Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null).Result);
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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
            
            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 3000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcPublishingInterval = 2000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcPublishingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.DefaultOpcSamplingInterval = 3000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.DefaultOpcSamplingInterval = 2000;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].OpcSamplingInterval == 2000);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.SkipFirstDefault = false;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.SkipFirstDefault = true;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.SkipFirstDefault = false;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == false);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.SkipFirstDefault = true;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].SkipFirst == true);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 0);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.HeartbeatIntervalDefault = 5;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;
            
            SettingsConfiguration.HeartbeatIntervalDefault = 2;

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            
            _configurationFileEntries = new List<ConfigurationFileEntryLegacyModel>();
            _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename));
            Assert.True(_configurationFileEntries[0].OpcNodes[0].HeartbeatInterval == 2);
            _uaClient.UnpublishAllNodes();
            Metrics.Clear();
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
        private List<ConfigurationFileEntryLegacyModel> _configurationFileEntries;
        private ApplicationInstance _application;
        private UAClient _uaClient;
    }
}
