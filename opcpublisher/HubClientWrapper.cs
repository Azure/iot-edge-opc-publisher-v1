// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using OpcPublisher.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpcPublisher
{
    /// <summary>
    /// Class to encapsulate the IoTHub device/module client interface.
    /// </summary>
    public class HubClientWrapper : IHubClientWrapper
    {
        /// <summary>
        /// Specifies the queue capacity for monitored item events.
        /// </summary>
        public int MonitoredItemsQueueCapacity { get; set; } = 8192;

        /// <summary>
        /// Number of events in the monitored items queue.
        /// </summary>
        public long MonitoredItemsQueueCount => _monitoredItemsDataQueue.Count;

        /// <summary>
        /// Number of events we enqueued.
        /// </summary>
        public long EnqueueCount => _enqueueCount;

        /// <summary>
        /// Number of times enqueueing of events failed.
        /// </summary>
        public long EnqueueFailureCount => _enqueueFailureCount;

        /// <summary>
        /// Specifies max message size in byte for hub communication allowed.
        /// </summary>
        public const uint HubMessageSizeMax = 256 * 1024;

        /// <summary>
        /// Specifies the message size in bytes used for hub communication.
        /// </summary>
        public uint HubMessageSize { get; set; } = HubMessageSizeMax;

        /// <summary>
        /// Specifies the send interval in seconds after which a message is sent to the hub.
        /// </summary>
        public int DefaultSendIntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Number of events sent to the cloud.
        /// </summary>
        public long NumberOfEvents { get; set; }

        /// <summary>
        /// Number of times we were not able to make the send interval, because too high load.
        /// </summary>
        public long MissedSendIntervalCount { get; set; }

        /// <summary>
        /// Number of times the isze fo the event payload was too large for a telemetry message.
        /// </summary>
        public long TooLargeCount { get; set; }

        /// <summary>
        /// Number of payload bytes we sent to the cloud.
        /// </summary>
        public long SentBytes { get; set; }

        /// <summary>
        /// Number of messages we sent to the cloud.
        /// </summary>
        public long SentMessages { get; set; }

        /// <summary>
        /// Time when we sent the last telemetry message.
        /// </summary>
        public DateTime SentLastTime { get; set; }

        /// <summary>
        /// Number of times we were not able to sent the telemetry message to the cloud.
        /// </summary>
        public long FailedMessages { get; set; }

        /// <summary>
        /// Allow to ingest data into IoT Central.
        /// </summary>
        public bool IotCentralMode { get; set; } = false;

        /// <summary>
        /// The protocol to use for hub communication.
        /// </summary>
        public const TransportType IotHubProtocol = TransportType.Mqtt_WebSocket_Only;
        public const TransportType EdgeHubProtocol = TransportType.Amqp_Tcp_Only;

        /// <summary>
        /// Stores custom product information that will be appended to the user agent string that is sent to IoT Hub.
        /// </summary>
        public virtual string ProductInfo
        {
            get
            {
                if (_iotHubClient == null)
                {
                    return _edgeHubClient.ProductInfo;
                }
                return _iotHubClient.ProductInfo;
            }
            set
            {
                if (_iotHubClient == null)
                {
                    _edgeHubClient.ProductInfo = value;
                    return;
                }
                _iotHubClient.ProductInfo = value;
            }
        }

        /// <summary>
        /// Singleton pattern
        /// </summary>
        public static HubClientWrapper Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }
                else
                {
                    lock (_singletonLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HubClientWrapper();
                        }
                        return _instance;
                    }
                }
            }
        }

        /// <summary>
        /// Private default constructor
        /// </summary>
        private HubClientWrapper()
        {
            // nothing to do
        }

        /// <summary>
        /// Close the client instance
        /// </summary>
        public virtual void Close()
        {
            // send cancellation token and wait for last IoT Hub message to be sent.
            _hubCommunicationCts?.Cancel();
            try
            {
                _monitoredItemsProcessorTask?.Wait();
                _monitoredItemsProcessorTask = null;

                if (_edgeHubClient != null)
                {
                    _edgeHubClient.CloseAsync();
                }

                if (_iotHubClient != null)
                {
                    _iotHubClient.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Program.Logger.Error(e, "Failure while shutting down hub messaging.");
            }

            _hubCommunicationCts?.Dispose();
            _hubCommunicationCts = null;
        }

        /// <summary>
        /// Handle connection status change notifications.
        /// </summary>
        private void ConnectionStatusChange(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            if (reason == ConnectionStatusChangeReason.Connection_Ok || Program.ShutdownTokenSource.IsCancellationRequested)
            {
                Program.Logger.Information($"Connection status changed to '{status}', reason '{reason}'");
            }
            else
            {
                Program.Logger.Error($"Connection status changed to '{status}', reason '{reason}'");
            }
        }

        /// <summary>
        /// Initializes edge message broker communication.
        /// </summary>
        public virtual void InitHubCommunication(bool runningInIoTEdgeContext, string connectionString)
        {
            _hubCommunicationCts = new CancellationTokenSource();
            _shutdownToken = _hubCommunicationCts.Token;

            ExponentialBackoff exponentialRetryPolicy = new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(1024), TimeSpan.FromMilliseconds(3));

            // show IoTCentral mode
            Program.Logger.Information($"IoTCentral mode: {IotCentralMode}");

            // open connection
            Program.Logger.Debug($"Open hub communication");
            if (runningInIoTEdgeContext)
            {
                Program.Logger.Information($"Create module client using '{EdgeHubProtocol}' for communication.");

                if (string.IsNullOrEmpty(connectionString))
                {
                    _edgeHubClient = ModuleClient.CreateFromEnvironmentAsync(EdgeHubProtocol).Result;
                }
                else
                {
                    _edgeHubClient = ModuleClient.CreateFromConnectionString(connectionString, EdgeHubProtocol);
                }

                HubMethodHandler.Instance.RegisterMethodHandlers(_edgeHubClient);
            }
            else
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    string errorMessage = $"Please pass the device connection string in via command line option. Can not connect to IoTHub. Exiting...";
                    Program.Logger.Fatal(errorMessage);
                    throw new ArgumentException(errorMessage);
                }

                Program.Logger.Information($"Create device client using '{IotHubProtocol}' for communication.");

                _iotHubClient = DeviceClient.CreateFromConnectionString(connectionString, IotHubProtocol);

                HubMethodHandler.Instance.RegisterMethodHandlers(_iotHubClient);
            }

            ProductInfo = "OpcPublisher";
            SetRetryPolicy(exponentialRetryPolicy);

            // register connection status change handler
            SetConnectionStatusChangesHandler(ConnectionStatusChange);

            Program.Logger.Debug($"Init D2C message processing");
            if (InitMessageProcessingAsync().Result == false)
            {
                string errorMessage = $"Failure initializing hub message processing";
                Program.Logger.Fatal(errorMessage);
                throw new Exception(errorMessage);
            }
        }

        /// <summary>
        /// Initializes internal message processing.
        /// </summary>
        private Task<bool> InitMessageProcessingAsync()
        {
            try
            {
                // show config
                Program.Logger.Information($"Message processing and hub communication configured with a send interval of {DefaultSendIntervalSeconds} sec and a message buffer size of {HubMessageSize} bytes.");

                // create the queue for monitored items
                _monitoredItemsDataQueue = new BlockingCollection<MessageDataModel>(MonitoredItemsQueueCapacity);

                // start up task to send telemetry to IoTHub
                _monitoredItemsProcessorTask = null;

                Program.Logger.Information("Creating task process and batch monitored item data updates...");
                _monitoredItemsProcessorTask = Task.Run(() => MonitoredItemsProcessorAsync(_shutdownToken).ConfigureAwait(false), _shutdownToken);
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Program.Logger.Error(e, "Failure initializing message processing.");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Creates a JSON message to be sent to IoTCentral.
        /// </summary>
        private async Task<string> CreateIotCentralJsonMessageAsync(MessageDataModel messageData)
        {
            try
            {
                // build the JSON message for IoTCentral
                StringBuilder _jsonStringBuilder = new StringBuilder();
                StringWriter _jsonStringWriter = new StringWriter(_jsonStringBuilder);
                using (JsonWriter _jsonWriter = new JsonTextWriter(_jsonStringWriter))
                {
                    await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                    await _jsonWriter.WritePropertyNameAsync(messageData.DisplayName).ConfigureAwait(false);
                    await _jsonWriter.WriteValueAsync(messageData.Value).ConfigureAwait(false);
                    await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    await _jsonWriter.FlushAsync().ConfigureAwait(false);
                }
                return _jsonStringBuilder.ToString();
            }
            catch (Exception e)
            {
                Program.Logger.Error(e, "Generation of IoTCentral JSON message failed.");
            }
            return string.Empty;
        }

        /// <summary>
        /// Creates a JSON message to be sent to IoTHub, based on the telemetry configuration for the endpoint.
        /// </summary>
        private async Task<string> CreateJsonMessageAsync(MessageDataModel messageData)
        {
            try
            {
                // get telemetry configration
                EndpointTelemetryConfigurationModel telemetryConfiguration = Program.TelemetryConfiguration.GetEndpointTelemetryConfiguration(messageData.EndpointUrl);

                // currently the pattern processing is done in MonitoredItemNotificationHandler of OpcSession.cs. in case of perf issues
                // it can be also done here, the risk is then to lose messages in the communication queue. if you enable it here, disable it in OpcSession.cs
                // messageData.ApplyPatterns(telemetryConfiguration);

                // build the JSON message
                StringBuilder _jsonStringBuilder = new StringBuilder();
                StringWriter _jsonStringWriter = new StringWriter(_jsonStringBuilder);
                using (JsonWriter _jsonWriter = new JsonTextWriter(_jsonStringWriter))
                {
                    await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                    string telemetryValue = string.Empty;

                    // process EndpointUrl
                    if ((bool)telemetryConfiguration.EndpointUrl.Publish)
                    {
                        await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.EndpointUrl.Name).ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(messageData.EndpointUrl).ConfigureAwait(false);
                    }

                    // process NodeId
                    if (!string.IsNullOrEmpty(messageData.NodeId))
                    {
                        await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.NodeId.Name).ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(messageData.NodeId).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(messageData.ExpandedNodeId))
                    {
                        await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.ExpandedNodeId.Name).ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(messageData.ExpandedNodeId).ConfigureAwait(false);
                    }

                    // process MonitoredItem object properties
                    if (!string.IsNullOrEmpty(messageData.ApplicationUri) || !string.IsNullOrEmpty(messageData.DisplayName))
                    {
                        if (!(bool)telemetryConfiguration.MonitoredItem.Flat)
                        {
                            await _jsonWriter.WritePropertyNameAsync("MonitoredItem").ConfigureAwait(false);
                            await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                        }

                        // process ApplicationUri
                        if (!string.IsNullOrEmpty(messageData.ApplicationUri))
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.MonitoredItem.ApplicationUri.Name).ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.ApplicationUri).ConfigureAwait(false);
                        }

                        // process DisplayName
                        if (!string.IsNullOrEmpty(messageData.DisplayName))
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.MonitoredItem.DisplayName.Name).ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.DisplayName).ConfigureAwait(false);
                        }

                        if (!(bool)telemetryConfiguration.MonitoredItem.Flat)
                        {
                            await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                        }
                    }

                    // process Value object properties
                    if (!string.IsNullOrEmpty(messageData.Value) || !string.IsNullOrEmpty(messageData.SourceTimestamp) ||
                       messageData.StatusCode != null || !string.IsNullOrEmpty(messageData.Status))
                    {
                        if (!(bool)telemetryConfiguration.Value.Flat)
                        {
                            await _jsonWriter.WritePropertyNameAsync("Value").ConfigureAwait(false);
                            await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                        }

                        // process Value
                        if (!string.IsNullOrEmpty(messageData.Value))
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.Value.Value.Name).ConfigureAwait(false);
                            if (messageData.PreserveValueQuotes)
                            {
                                await _jsonWriter.WriteValueAsync(messageData.Value).ConfigureAwait(false);
                            }
                            else
                            {
                                await _jsonWriter.WriteRawValueAsync(messageData.Value).ConfigureAwait(false);
                            }
                        }

                        // process SourceTimestamp
                        if (!string.IsNullOrEmpty(messageData.SourceTimestamp))
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.Value.SourceTimestamp.Name).ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.SourceTimestamp).ConfigureAwait(false);
                        }

                        // process StatusCode
                        if (messageData.StatusCode != null)
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.Value.StatusCode.Name).ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.StatusCode).ConfigureAwait(false);
                        }

                        // process Status
                        if (!string.IsNullOrEmpty(messageData.Status))
                        {
                            await _jsonWriter.WritePropertyNameAsync(telemetryConfiguration.Value.Status.Name).ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.Status).ConfigureAwait(false);
                        }

                        if (!(bool)telemetryConfiguration.Value.Flat)
                        {
                            await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                        }
                    }
                    await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    await _jsonWriter.FlushAsync().ConfigureAwait(false);
                }
                return _jsonStringBuilder.ToString();
            }
            catch (Exception e)
            {
                Program.Logger.Error(e, "Generation of JSON message failed.");
            }
            return string.Empty;
        }

        /// <summary>
        /// Dequeue monitored item notification messages, batch them for send (if needed) and send them to IoTHub.
        /// </summary>
        public virtual async Task MonitoredItemsProcessorAsync(CancellationToken ct)
        {
            uint jsonSquareBracketLength = 2;
            Message tempMsg = new Message();
            // the system properties are MessageId (max 128 byte), Sequence number (ulong), ExpiryTime (DateTime) and more. ideally we get that from the client.
            int systemPropertyLength = 128 + sizeof(ulong) + tempMsg.ExpiryTimeUtc.ToString(CultureInfo.InvariantCulture).Length;
            int applicationPropertyLength = Encoding.UTF8.GetByteCount($"iothub-content-type={CONTENT_TYPE_OPCUAJSON}") + Encoding.UTF8.GetByteCount($"iothub-content-encoding={CONTENT_ENCODING_UTF8}");
            // if batching is requested the buffer will have the requested size, otherwise we reserve the max size
            uint hubMessageBufferSize = (HubMessageSize > 0 ? HubMessageSize : HubMessageSizeMax) - (uint)systemPropertyLength - jsonSquareBracketLength - (uint)applicationPropertyLength;
            byte[] hubMessageBuffer = new byte[hubMessageBufferSize];
            MemoryStream hubMessage = new MemoryStream(hubMessageBuffer);
            var nextSendTime = DateTime.UtcNow + TimeSpan.FromSeconds(DefaultSendIntervalSeconds);
            bool singleMessageSend = DefaultSendIntervalSeconds == 0 && HubMessageSize == 0;

            using (hubMessage)
            {
                try
                {
                    string jsonMessage = string.Empty;
                    MessageDataModel messageData = new MessageDataModel();
                    bool needToBufferMessage = false;
                    int jsonMessageSize = 0;

                    hubMessage.Position = 0;
                    hubMessage.SetLength(0);
                    if (!singleMessageSend)
                    {
                        hubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                    }
                    while (true)
                    {
                        TimeSpan timeTillNextSend;
                        int millisToWait;
                        // sanity check the send interval, compute the timeout and get the next monitored item message
                        if (DefaultSendIntervalSeconds > 0)
                        {
                            timeTillNextSend = nextSendTime.Subtract(DateTime.UtcNow);
                            if (timeTillNextSend < TimeSpan.Zero)
                            {
                                MissedSendIntervalCount++;
                                // do not wait if we missed the send interval
                                timeTillNextSend = TimeSpan.Zero;
                            }

                            long millisLong = (long)timeTillNextSend.TotalMilliseconds;
                            if (millisLong < 0 || millisLong > int.MaxValue)
                            {
                                millisToWait = 0;
                            }
                            else
                            {
                                millisToWait = (int)millisLong;
                            }
                        }
                        else
                        {
                            // if we are in shutdown do not wait, else wait infinite if send interval is not set
                            millisToWait = ct.IsCancellationRequested ? 0 : Timeout.Infinite;
                        }
                        bool gotItem = _monitoredItemsDataQueue.TryTake(out messageData, millisToWait, ct);

                        // the two commandline parameter --ms (message size) and --si (send interval) control when data is sent to IoTHub/EdgeHub
                        // pls see detailed comments on performance and memory consumption at https://github.com/Azure/iot-edge-opc-publisher

                        // check if we got an item or if we hit the timeout or got canceled
                        if (gotItem)
                        {
                            if (IotCentralMode)
                            {
                                // for IoTCentral we send simple key/value pairs. key is the DisplayName, value the value.
                                jsonMessage = await CreateIotCentralJsonMessageAsync(messageData).ConfigureAwait(false);
                            }
                            else
                            {
                                // create a JSON message from the messageData object
                                jsonMessage = await CreateJsonMessageAsync(messageData).ConfigureAwait(false);
                            }

                            NumberOfEvents++;
                            jsonMessageSize = Encoding.UTF8.GetByteCount(jsonMessage);

                            // sanity check that the user has set a large enough messages size
                            if ((HubMessageSize > 0 && jsonMessageSize > HubMessageSize) || (HubMessageSize == 0 && jsonMessageSize > hubMessageBufferSize))
                            {
                                Program.Logger.Error($"There is a telemetry message (size: {jsonMessageSize}), which will not fit into an hub message (max size: {hubMessageBufferSize}].");
                                Program.Logger.Error($"Please check your hub message size settings. The telemetry message will be discarded silently. Sorry:(");
                                TooLargeCount++;
                                continue;
                            }

                            // if batching is requested or we need to send at intervals, batch it otherwise send it right away
                            needToBufferMessage = false;
                            if (HubMessageSize > 0 || (HubMessageSize == 0 && DefaultSendIntervalSeconds > 0))
                            {
                                // if there is still space to batch, do it. otherwise send the buffer and flag the message for later buffering
                                if (hubMessage.Position + jsonMessageSize + 1 <= hubMessage.Capacity)
                                {
                                    // add the message and a comma to the buffer
                                    hubMessage.Write(Encoding.UTF8.GetBytes(jsonMessage), 0, jsonMessageSize);
                                    hubMessage.Write(Encoding.UTF8.GetBytes(","), 0, 1);
                                    Program.Logger.Debug($"Added new message with size {jsonMessageSize} to hub message (size is now {hubMessage.Position - 1}).");
                                    continue;
                                }
                                else
                                {
                                    needToBufferMessage = true;
                                }
                            }
                        }
                        else
                        {
                            // if we got no message, we either reached the interval or we are in shutdown and have processed all messages
                            if (ct.IsCancellationRequested)
                            {
                                Program.Logger.Information($"Cancellation requested.");
                                _monitoredItemsDataQueue.CompleteAdding();
                                _monitoredItemsDataQueue.Dispose();
                                break;
                            }
                        }

                        // the batching is completed or we reached the send interval or got a cancelation request
                        try
                        {
                            Microsoft.Azure.Devices.Client.Message encodedhubMessage = null;

                            // if we reached the send interval, but have nothing to send (only the opening square bracket is there), we continue
                            if (!gotItem && hubMessage.Position == 1)
                            {
                                Program.Logger.Verbose("Adding {seconds} seconds to current nextSendTime {nextSendTime}...", DefaultSendIntervalSeconds, nextSendTime);
                                nextSendTime += TimeSpan.FromSeconds(DefaultSendIntervalSeconds);
                                hubMessage.Position = 0;
                                hubMessage.SetLength(0);
                                if (!singleMessageSend)
                                {
                                    hubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                                }
                                continue;
                            }

                            // if there is no batching and no send interval configured, we send the JSON message we just got, otherwise we send the buffer
                            if (singleMessageSend)
                            {
                                // create the message without brackets
                                encodedhubMessage = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                            }
                            else
                            {
                                // remove the trailing comma and add a closing square bracket
                                hubMessage.SetLength(hubMessage.Length - 1);
                                hubMessage.Write(Encoding.UTF8.GetBytes("]"), 0, 1);
                                encodedhubMessage = new Message(hubMessage.ToArray());
                            }
                            
                            encodedhubMessage.ContentType = CONTENT_TYPE_OPCUAJSON;
                            encodedhubMessage.ContentEncoding = CONTENT_ENCODING_UTF8;

                            if (nextSendTime < DateTime.UtcNow)
                            {
                                Program.Logger.Verbose("Adding {seconds} seconds to current nextSendTime {nextSendTime}...", DefaultSendIntervalSeconds, nextSendTime);
                                nextSendTime += TimeSpan.FromSeconds(DefaultSendIntervalSeconds);
                            }

                            try
                            {
                                SentBytes += encodedhubMessage.GetBytes().Length;
                                await SendEventAsync(encodedhubMessage).ConfigureAwait(false);
                                SentMessages++;
                                SentLastTime = DateTime.UtcNow;
                                Program.Logger.Debug($"Sending {encodedhubMessage.BodyStream.Length} bytes to hub.");
                            }
                            catch
                            {
                                FailedMessages++;
                            }

                            // reset the messaage
                            hubMessage.Position = 0;
                            hubMessage.SetLength(0);
                            if (!singleMessageSend)
                            {
                                hubMessage.Write(Encoding.UTF8.GetBytes("["), 0, 1);
                            }

                            // if we had not yet buffered the last message because there was not enough space, buffer it now
                            if (needToBufferMessage)
                            {
                                // add the message and a comma to the buffer
                                hubMessage.Write(Encoding.UTF8.GetBytes(jsonMessage), 0, jsonMessageSize);
                                hubMessage.Write(Encoding.UTF8.GetBytes(","), 0, 1);
                            }
                        }
                        catch (Exception e)
                        {
                            Program.Logger.Error(e, "Exception while sending message to hub. Dropping message...");
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!(e is OperationCanceledException))
                    {
                        Program.Logger.Error(e, "Error while processing monitored item messages.");
                    }
                }
            }
        }

        /// <summary>
        /// Sets the retry policy used in the operation retries.
        /// </summary>
        public virtual void SetRetryPolicy(IRetryPolicy retryPolicy)
        {
            if (_iotHubClient == null)
            {
                _edgeHubClient.SetRetryPolicy(retryPolicy);
                return;
            }
            _iotHubClient.SetRetryPolicy(retryPolicy);
        }

        /// <summary>
        /// Registers a new delegate for the connection status changed callback. If a delegate is already associated,
        /// it will be replaced with the new delegate.
        /// </summary>
        public virtual void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler)
        {
            if (_iotHubClient == null)
            {
                _edgeHubClient.SetConnectionStatusChangesHandler(statusChangesHandler);
                return;
            }
            _iotHubClient.SetConnectionStatusChangesHandler(statusChangesHandler);
        }

        /// <summary>
        /// Sends an event to device hub
        /// </summary>
        public virtual Task SendEventAsync(Message message)
        {
            if (_iotHubClient == null)
            {
                return _edgeHubClient.SendEventAsync(message);
            }
            return _iotHubClient.SendEventAsync(message);
        }

        /// <summary>
        /// Enqueue a message for sending to IoTHub.
        /// </summary>
        public virtual void Enqueue(MessageDataModel json)
        {
            // Try to add the message.
            Interlocked.Increment(ref _enqueueCount);
            if (_monitoredItemsDataQueue.TryAdd(json) == false)
            {
                Interlocked.Increment(ref _enqueueFailureCount);
                if (_enqueueFailureCount % 10000 == 0)
                {
                    Program.Logger.Information($"The internal monitored item message queue is above its capacity of {_monitoredItemsDataQueue.BoundedCapacity}. We have already lost {_enqueueFailureCount} monitored item notifications:(");
                }
            }
        }

        private const string CONTENT_TYPE_OPCUAJSON = "application/opcua+uajson";
        private const string CONTENT_ENCODING_UTF8 = "UTF-8";
        
        private long _enqueueCount;
        private long _enqueueFailureCount;
        private BlockingCollection<MessageDataModel> _monitoredItemsDataQueue;

        private Task _monitoredItemsProcessorTask;
        private CancellationTokenSource _hubCommunicationCts;
        private CancellationToken _shutdownToken;

        private DeviceClient _iotHubClient;
        private ModuleClient _edgeHubClient;

        private static readonly object _singletonLock = new object();
        private static HubClientWrapper _instance = null;
    }
}
