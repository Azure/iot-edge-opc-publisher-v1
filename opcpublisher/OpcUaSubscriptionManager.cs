// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace OpcPublisher
{
    /// <summary>
    /// Class to manage OPC subscriptions. We create a subscription for each different publishing interval
    /// on an endpoint.
    /// </summary>
    public class OpcUaSubscriptionManager
    {
        /// <summary>
        /// List of monitored items on this subscription.
        /// </summary>
        public List<OpcUaMonitoredItemManager> OpcMonitoredItems { get; }

        /// <summary>
        /// The OPC UA stack subscription object.
        /// </summary>
        public OpcUaSubscriptionWrapper OpcUaClientSubscription { get; set; }

        /// <summary>
        /// The publishing interval requested to be used for the subscription.
        /// </summary>
        public int RequestedPublishingInterval { get; set; }

        /// <summary>
        /// The actual publishing interval used for the subscription.
        /// </summary>
        public double PublishingInterval { get; set; }

        /// <summary>
        /// Flag to signal that the publishing interval was requested by the node configuration.
        /// </summary>
        public bool RequestedPublishingIntervalFromConfiguration { get; set; }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        /// <param name="publishingInterval"></param>
        public OpcUaSubscriptionManager(int? publishingInterval)
        {
            OpcMonitoredItems = new List<OpcUaMonitoredItemManager>();
            RequestedPublishingInterval = publishingInterval ?? Configurations.OpcApplicationConfiguration.OpcPublishingInterval;
            RequestedPublishingIntervalFromConfiguration = publishingInterval != null ? true : false;
            PublishingInterval = RequestedPublishingInterval;
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                foreach (var opcMonitoredItem in OpcMonitoredItems)
                {
                    opcMonitoredItem?.HeartbeatSendTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                }
                OpcMonitoredItems?.Clear();
                OpcUaClientSubscription?.Dispose();
                OpcUaClientSubscription = null;
            }
        }

        /// <summary>
        /// Implement IDisposable.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
