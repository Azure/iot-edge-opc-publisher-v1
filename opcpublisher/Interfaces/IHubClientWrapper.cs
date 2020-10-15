// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using System.Threading;
using System.Threading.Tasks;

namespace OpcPublisher.Interfaces
{
    /// <summary>
    /// Interface to encapsulate the IoTHub device/module client interface.
    /// </summary>
    public interface IHubClientWrapper
    {
        /// <summary>
        /// Stores custom product information that will be appended to the user agent string that is sent to IoT Hub.
        /// </summary>
        string ProductInfo { get; set; }

        /// <summary>
        /// Initializes the hub communication.
        /// </summary>
        void InitHubCommunication(bool runningInIoTEdgeContext, string connectionString);

        /// <summary>
        /// Close the client instance
        /// </summary>
        void Close();

        /// <summary>
        /// Sets the retry policy used in the operation retries.
        /// </summary>
        void SetRetryPolicy(IRetryPolicy retryPolicy);

        /// <summary>
        /// Registers a new delegate for the connection status changed callback. If a delegate is already associated, 
        /// it will be replaced with the new delegate.
        /// </summary>
        void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler);

        /// <summary>
        /// Sends an event to device hub
        /// </summary>
        Task SendEventAsync(Message message);

        /// <summary>
        /// Enqueue a message for sending to IoTHub.
        /// </summary>
        void Enqueue(MessageDataModel json);

        /// <summary>
        /// Dequeue monitored item notification messages, batch them for send (if needed) and send them to IoTHub.
        /// </summary>
        Task MonitoredItemsProcessorAsync(CancellationToken ct);
    }
}
