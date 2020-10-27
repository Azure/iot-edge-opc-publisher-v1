// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);
            
            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));
                        
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(2500).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started");
            Assert.Equal(3, eventsAfterDelay - eventsAtStart);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(3000).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            Assert.Equal(1, eventsAfterDelay - eventsAtStart);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();
            
            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(1900).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            Assert.True(eventsAfterDelay - eventsAtStart == 1);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(3000).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            Assert.True(eventsAfterDelay - eventsAtStart == 0);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitored();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(5000).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            Assert.Equal(2, eventsAfterDelay - eventsAtStart);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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

            ApplicationInstance _application = new ApplicationInstance {
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

            UAClient _uaClient = new UAClient(_application.ApplicationConfiguration);
            HubMethodHandler hubMethodHandler = new HubMethodHandler(_uaClient);

            UnitTestHelper.SetPublisherDefaults();
            SettingsConfiguration.PublisherNodeConfigurationFilename = fqTempFilename;

            HubClientWrapper hubClientWrapper = new HubClientWrapper();
            hubClientWrapper.InitMessageProcessing();

            // wait 5 seconds for the server to become available
            await Task.Delay(5000);

            PublishedNodesConfiguration.ReadConfig(_uaClient, await _application.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null));

            _output.WriteLine($"now testing: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
            Assert.True(File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename));

            int eventsReceived = 0;
            long eventsAtStart = Metrics.NumberOfEvents;
            
            Assert.True(Metrics.NumberOfOpcSessionsConnected == configuredSessions, "wrong # of sessions");
            Assert.True(Metrics.NumberOfOpcSubscriptionsConnected == configuredSubscriptions, "wrong # of subscriptions");
            Assert.True(Metrics.NumberOfOpcMonitoredItemsMonitored == configuredMonitoredItems, "wrong # of monitored items");
            int seconds = UnitTestHelper.WaitTilItemsAreMonitoredAndFirstEventReceived();
            long eventsAfterConnect = Metrics.NumberOfEvents;
            await Task.Delay(3000).ConfigureAwait(false);
            long eventsAfterDelay = Metrics.NumberOfEvents;
            _output.WriteLine($"# of events at start: {eventsAtStart}, # events after connect: {eventsAfterConnect}, # events after delay: {eventsAfterDelay}");
            _output.WriteLine($"sessions configured {Metrics.NumberOfOpcSessionsConnected}, connected {Metrics.NumberOfOpcSessionsConnected}");
            _output.WriteLine($"subscriptions configured {Metrics.NumberOfOpcSubscriptionsConnected}, connected {Metrics.NumberOfOpcSubscriptionsConnected}");
            _output.WriteLine($"items configured {Metrics.NumberOfOpcMonitoredItemsMonitored}, monitored {Metrics.NumberOfOpcMonitoredItemsMonitored}");
            _output.WriteLine($"waited {seconds} seconds till monitoring started, events generated {eventsReceived}");
            Assert.True(eventsAfterDelay - eventsAtStart == 2);
            _uaClient.UnpublishAllNodes();
            hubClientWrapper.Close();
            Metrics.Clear();
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
