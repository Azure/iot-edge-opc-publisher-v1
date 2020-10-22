// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua.Client;
using OpcPublisher.Configurations;
using System.Collections.Generic;

namespace OpcPublisher
{
    /// <summary>
    /// Wrapper of an OPC subscription. We create a subscription for each publishing interval
    /// on an endpoint.
    /// </summary>
    public class OpcUaSubscriptionWrapper
    {
        /// <summary>
        /// List of monitored items on this subscription.
        /// </summary>
        public List<OpcUaMonitoredItemWrapper> OpcMonitoredItems = new List<OpcUaMonitoredItemWrapper>();

        /// <summary>
        /// The OPC UA stack subscription object.
        /// </summary>
        public Subscription OpcUaClientSubscription;

        public int PublishingInterval;

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        /// <param name="publishingInterval"></param>
        public OpcUaSubscriptionWrapper(int? publishingInterval)
        {
            PublishingInterval = publishingInterval?? SettingsConfiguration.DefaultOpcPublishingInterval;
        }
    }
}
