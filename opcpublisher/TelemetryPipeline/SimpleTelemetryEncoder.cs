// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OpcPublisher
{
    class SimpleTelemetryEncoder
    {
        /// <summary>
        /// Creates a JSON message to be sent to IoTHub, based on the telemetry configuration for the endpoint.
        /// </summary>
        public static async Task<string> CreateJsonForDataChangeAsync(MessageDataModel messageData)
        {
            try
            {
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
                    await _jsonWriter.WritePropertyNameAsync("EndpointUrl").ConfigureAwait(false);
                    await _jsonWriter.WriteValueAsync(messageData.EndpointUrl).ConfigureAwait(false);

                    // process NodeId
                    if (!string.IsNullOrEmpty(messageData.NodeId))
                    {
                        await _jsonWriter.WritePropertyNameAsync("NodeId").ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(messageData.NodeId).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(messageData.ExpandedNodeId))
                    {
                        await _jsonWriter.WritePropertyNameAsync("ExpandedNodeId").ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(messageData.ExpandedNodeId).ConfigureAwait(false);
                    }

                    // process MonitoredItem object properties
                    if (!string.IsNullOrEmpty(messageData.ApplicationUri) || !string.IsNullOrEmpty(messageData.DisplayName))
                    {
                        await _jsonWriter.WritePropertyNameAsync("MonitoredItem").ConfigureAwait(false);
                        await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

                        // process ApplicationUri
                        if (!string.IsNullOrEmpty(messageData.ApplicationUri))
                        {
                            await _jsonWriter.WritePropertyNameAsync("ApplicationUri").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.ApplicationUri).ConfigureAwait(false);
                        }

                        // process DisplayName
                        if (!string.IsNullOrEmpty(messageData.DisplayName))
                        {
                            await _jsonWriter.WritePropertyNameAsync("DisplayName").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.DisplayName).ConfigureAwait(false);
                        }

                        await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }

                    // process Value object properties
                    if (!string.IsNullOrEmpty(messageData.Value) || !string.IsNullOrEmpty(messageData.SourceTimestamp) ||
                       messageData.StatusCode != null || !string.IsNullOrEmpty(messageData.Status))
                    {
                        await _jsonWriter.WritePropertyNameAsync("Value").ConfigureAwait(false);
                        await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

                        // process Value
                        if (!string.IsNullOrEmpty(messageData.Value))
                        {
                            await _jsonWriter.WritePropertyNameAsync("Value").ConfigureAwait(false);
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
                            await _jsonWriter.WritePropertyNameAsync("SourceTimestamp").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.SourceTimestamp).ConfigureAwait(false);
                        }

                        // process StatusCode
                        if (messageData.StatusCode != null)
                        {
                            await _jsonWriter.WritePropertyNameAsync("StatusCode").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.StatusCode).ConfigureAwait(false);
                        }

                        // process Status
                        if (!string.IsNullOrEmpty(messageData.Status))
                        {
                            await _jsonWriter.WritePropertyNameAsync("Status").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(messageData.Status).ConfigureAwait(false);
                        }

                        await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }

                    await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    await _jsonWriter.FlushAsync().ConfigureAwait(false);
                }

                return _jsonStringBuilder.ToString();
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Generation of JSON message failed.");
            }
            return string.Empty;
        }

        /// <summary>
        /// Creates a IoTHub JSON message for an event notification, based on the telemetry configuration for the endpoint.
        /// </summary>
        public static async Task<string> CreateJsonForEventAsync(EventMessageDataModel eventData)
        {
            try
            {
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
                    await _jsonWriter.WritePropertyNameAsync("EndpointUrl").ConfigureAwait(false);
                    await _jsonWriter.WriteValueAsync(eventData.EndpointUrl).ConfigureAwait(false);

                    // process NodeId
                    if (!string.IsNullOrEmpty(eventData.NodeId))
                    {
                        await _jsonWriter.WritePropertyNameAsync("NodeId").ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(eventData.NodeId).ConfigureAwait(false);
                    }

                    // process MonitoredItem object properties
                    if (!string.IsNullOrEmpty(eventData.ApplicationUri) || !string.IsNullOrEmpty(eventData.DisplayName))
                    {
                        await _jsonWriter.WritePropertyNameAsync("MonitoredItem").ConfigureAwait(false);
                        await _jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

                        // process ApplicationUri
                        if (!string.IsNullOrEmpty(eventData.ApplicationUri))
                        {
                            await _jsonWriter.WritePropertyNameAsync("ApplicationUri").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(eventData.ApplicationUri).ConfigureAwait(false);
                        }

                        // process DisplayName
                        if (!string.IsNullOrEmpty(eventData.DisplayName))
                        {
                            await _jsonWriter.WritePropertyNameAsync("DisplayName").ConfigureAwait(false);
                            await _jsonWriter.WriteValueAsync(eventData.DisplayName).ConfigureAwait(false);
                        }

                        await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    }

                    // process EventValues object properties
                    if (eventData.EventValues != null && eventData.EventValues.Count > 0)
                    {
                        foreach (var eventValue in eventData.EventValues)
                        {
                            await _jsonWriter.WritePropertyNameAsync(eventValue.Name).ConfigureAwait(false);
                            if (eventValue.PreserveValueQuotes)
                            {
                                await _jsonWriter.WriteValueAsync(eventValue.Value).ConfigureAwait(false);
                            }
                            else
                            {
                                await _jsonWriter.WriteRawValueAsync(eventValue.Value).ConfigureAwait(false);
                            }
                        }
                    }

                    // process PublishTime
                    if (!string.IsNullOrEmpty(eventData.PublishTime))
                    {
                        await _jsonWriter.WritePropertyNameAsync("PublishTime").ConfigureAwait(false);
                        await _jsonWriter.WriteValueAsync(eventData.PublishTime).ConfigureAwait(false);
                    }
                    await _jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                    await _jsonWriter.FlushAsync().ConfigureAwait(false);
                }

                return _jsonStringBuilder.ToString();
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Generation of JSON message failed.");
            }
            return string.Empty;
        }
    }
}
