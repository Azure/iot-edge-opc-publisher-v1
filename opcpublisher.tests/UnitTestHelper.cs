// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using OpcPublisher.Configurations;
using System.Threading;

namespace OpcPublisher
{
    /// <summary>
    /// Class with unit test helper methods.
    /// </summary>
    public static class UnitTestHelper
    {
        public static string GetMethodName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            return memberName;
        }

        public static int WaitTilItemsAreMonitored(PublishedNodesConfiguration nodeConfig)
        {
            // wait till monitoring starts
            int iter = 0;
            int startNum = nodeConfig.NumberOfOpcMonitoredItemsMonitored;
            while (nodeConfig.NumberOfOpcMonitoredItemsMonitored  == 0 && iter < _maxIterations)
            {
                Thread.Sleep(_sleepMilliseconds);
                iter++;
            }
            return iter < _maxIterations ? iter * _sleepMilliseconds / 1000 : -1;
        }
        public static int WaitTilItemsAreMonitoredAndFirstEventReceived(PublishedNodesConfiguration nodeConfig)
        {
            // wait till monitoring starts
            int iter = 0;
            long numberOfEventsStart = PublisherDiagnostics.NumberOfEvents;
            while ((nodeConfig.NumberOfOpcMonitoredItemsMonitored == 0 || (PublisherDiagnostics.NumberOfEvents - numberOfEventsStart) == 0) && iter < _maxIterations)
            {
                Thread.Sleep(_sleepMilliseconds);
                iter++;
            }
            return iter < _maxIterations ? iter * _sleepMilliseconds / 1000 : -1;
        }

        public static void SetPublisherDefaults()
        {
            SettingsConfiguration.DefaultOpcSamplingInterval = 1000;
            SettingsConfiguration.DefaultOpcPublishingInterval = 0;
            SettingsConfiguration.DefaultSendIntervalSeconds = 0;
            SettingsConfiguration.HubMessageSize = 0;
            SettingsConfiguration.SkipFirstDefault = false;
            SettingsConfiguration.HeartbeatIntervalDefault = 0;
        }

        private const int _maxTimeSeconds = 30;
        private const int _sleepMilliseconds = 100;
        private const int _maxIterations = _maxTimeSeconds * 1000 / _sleepMilliseconds;
    }
}
