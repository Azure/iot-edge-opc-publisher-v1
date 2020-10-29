// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Client;
using OpcPublisher.Configurations;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpcPublisher
{
    /// <summary>
    /// Wrapper for the OPC UA monitored item, which monitored a nodes we need to publish.
    /// </summary>
    public class MonitoredItemNotification
    {
        /// <summary>
        /// Skip first notification dictionary
        /// </summary>
        public static Dictionary<string, bool> SkipFirst { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// The notification that a monitored item event has occured on an OPC UA server.
        /// </summary>
        public static void MonitoredItemEventNotificationEventHandler(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                if (e == null || e.NotificationValue == null || monitoredItem == null || monitoredItem.Subscription == null || monitoredItem.Subscription.Session == null)
                {
                    return;
                }

                if (!(e.NotificationValue is EventFieldList notificationValue))
                {
                    return;
                }

                if (!(notificationValue.Message is NotificationMessage message))
                {
                    return;
                }

                if (!(message.NotificationData is ExtensionObjectCollection notificationData) || notificationData.Count == 0)
                {
                    return;
                }

                EventMessageDataModel eventMessageData = new EventMessageDataModel();
                eventMessageData.EndpointUrl = monitoredItem.Subscription.Session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
                eventMessageData.PublishTime = message.PublishTime.ToString("o", CultureInfo.InvariantCulture);
                eventMessageData.ApplicationUri = monitoredItem.Subscription.Session.Endpoint.Server.ApplicationUri + (string.IsNullOrEmpty(SettingsConfiguration.PublisherSite) ? "" : $":{SettingsConfiguration.PublisherSite}");
                eventMessageData.DisplayName = monitoredItem.DisplayName;
                eventMessageData.NodeId = monitoredItem.StartNodeId.ToString();
                foreach (var eventList in notificationData)
                {
                    EventNotificationList eventNotificationList = eventList.Body as EventNotificationList;
                    foreach (var eventFieldList in eventNotificationList.Events)
                    {
                        int i = 0;
                        foreach (var eventField in eventFieldList.EventFields)
                        {
                            // prepare event field values
                            EventValue eventValue = new EventValue();
                            eventValue.Name = monitoredItem.GetFieldName(i++);

                            // use the Value as reported in the notification event argument encoded with the OPC UA JSON endcoder
                            DataValue value = new DataValue(eventField);
                            string encodedValue = string.Empty;
                            EncodeValue(value, monitoredItem.Subscription.Session.MessageContext, out encodedValue, out bool preserveValueQuotes);
                            eventValue.Value = encodedValue;
                            eventValue.PreserveValueQuotes = preserveValueQuotes;
                            eventMessageData.EventValues.Add(eventValue);
                        }
                    }
                }

                // add message to fifo send queue
                if (monitoredItem.Subscription == null)
                {
                    Program.Instance.Logger.Debug($"Subscription already removed. No more details available.");
                }
                else
                {
                    Program.Instance.Logger.Debug($"Enqueue a new message from subscription {(monitoredItem.Subscription == null ? "removed" : monitoredItem.Subscription.Id.ToString(CultureInfo.InvariantCulture))}");
                    Program.Instance.Logger.Debug($" with publishing interval: {monitoredItem?.Subscription?.PublishingInterval} and sampling interval: {monitoredItem?.SamplingInterval}):");
                }

                // enqueue the telemetry event
                HubClientWrapper.Enqueue(eventMessageData);
            }
            catch (Exception ex)
            {
                Program.Instance.Logger.Error(ex, "Error processing monitored item notification");
            }
        }

        /// <summary>
        /// Encode a value and returns is as string. If the value is a string with quotes, we need to preserve the quotes.
        /// </summary>
        private static void EncodeValue(DataValue value, ServiceMessageContext messageContext, out string encodedValue, out bool preserveValueQuotes)
        {
            // use the Value as reported in the notification event argument encoded with the OPC UA JSON endcoder
            JsonEncoder encoder = new JsonEncoder(messageContext, false);
            value.ServerTimestamp = DateTime.MinValue;
            value.SourceTimestamp = DateTime.MinValue;
            value.StatusCode = StatusCodes.Good;
            encoder.WriteDataValue("Value", value);
            string valueString = encoder.CloseAndReturnText();
            // we only want the value string, search for everything till the real value starts
            // and get it
            string marker = "{\"Value\":{\"Value\":";
            int markerStart = valueString.IndexOf(marker, StringComparison.InvariantCulture);
            preserveValueQuotes = true;
            if (markerStart >= 0)
            {
                // we either have a value in quotes or just a value
                int valueLength;
                int valueStart = marker.Length;
                if (valueString.IndexOf("\"", valueStart, StringComparison.InvariantCulture) >= 0)
                {
                    // value is in quotes and two closing curly brackets at the end
                    valueStart++;
                    valueLength = valueString.Length - valueStart - 3;
                }
                else
                {
                    // value is without quotes with two curly brackets at the end
                    valueLength = valueString.Length - marker.Length - 2;
                    preserveValueQuotes = false;
                }
                encodedValue = valueString.Substring(valueStart, valueLength);
            }
            else
            {
                encodedValue = string.Empty;
            }
        }

        /// <summary>
        /// The notification that the data for a monitored item has changed on an OPC UA server.
        /// </summary>
        public static void DataChangedEventHandler(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
        {
            try
            {
                if (e == null || e.NotificationValue == null || monitoredItem == null || monitoredItem.Subscription == null || monitoredItem.Subscription.Session == null)
                {
                    return;
                }

                if (!(e.NotificationValue is Opc.Ua.MonitoredItemNotification notification))
                {
                    return;
                }

                if (!(notification.Value is DataValue value))
                {
                    return;
                }

                // filter out configured suppression status codes
                if (SettingsConfiguration.SuppressedOpcStatusCodes != null && SettingsConfiguration.SuppressedOpcStatusCodes.Contains(notification.Value.StatusCode.Code))
                {
                    Program.Instance.Logger.Debug($"Filtered notification with status code '{notification.Value.StatusCode.Code}'");
                    return;
                }

                MessageDataModel messageData = new MessageDataModel();
                messageData.EndpointUrl = monitoredItem.Subscription.Session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
                messageData.NodeId = monitoredItem.ResolvedNodeId.ToString();
                messageData.ApplicationUri = monitoredItem.Subscription.Session.Endpoint.Server.ApplicationUri + (string.IsNullOrEmpty(SettingsConfiguration.PublisherSite) ? "" : $":{SettingsConfiguration.PublisherSite}");
                
                if (monitoredItem.DisplayName != null)
                {
                    // use the DisplayName as reported in the MonitoredItem
                    messageData.DisplayName = monitoredItem.DisplayName;
                }
                
                // use the SourceTimestamp as reported in the notification event argument in ISO8601 format
                messageData.SourceTimestamp = value.SourceTimestamp.ToString("o", CultureInfo.InvariantCulture);
                
                // use the StatusCode as reported in the notification event argument
                messageData.StatusCode = value.StatusCode.Code;
                
                // use the StatusCode as reported in the notification event argument to lookup the symbolic name
                messageData.Status = StatusCode.LookupSymbolicId(value.StatusCode.Code);
                
                if (value.Value != null)
                {
                    string encodedValue = string.Empty;
                    EncodeValue(value, monitoredItem.Subscription.Session.MessageContext, out encodedValue, out bool preserveValueQuotes);
                    messageData.Value = encodedValue;
                    messageData.PreserveValueQuotes = preserveValueQuotes;
                }

                Program.Instance.Logger.Debug($"   ApplicationUri: {messageData.ApplicationUri}");
                Program.Instance.Logger.Debug($"   EndpointUrl: {messageData.EndpointUrl}");
                Program.Instance.Logger.Debug($"   DisplayName: {messageData.DisplayName}");
                Program.Instance.Logger.Debug($"   Value: {messageData.Value}");
                
                if (monitoredItem.Subscription == null)
                {
                    Program.Instance.Logger.Debug($"Subscription already removed. No more details available.");
                }
                else
                {
                    Program.Instance.Logger.Debug($"Enqueue a new message from subscription {(monitoredItem.Subscription == null ? "removed" : monitoredItem.Subscription.Id.ToString(CultureInfo.InvariantCulture))}");
                    Program.Instance.Logger.Debug($" with publishing interval: {monitoredItem?.Subscription?.PublishingInterval} and sampling interval: {monitoredItem?.SamplingInterval}):");
                }

                // skip event if needed
                if (SkipFirst.ContainsKey(messageData.NodeId) && SkipFirst[messageData.NodeId])
                {
                    Program.Instance.Logger.Debug($"Skipping first telemetry event for node '{messageData.DisplayName}'.");
                    SkipFirst[messageData.NodeId] = false;
                }
                else
                {
                    // enqueue the telemetry event
                    HubClientWrapper.Enqueue(messageData);
                }
            }
            catch (Exception ex)
            {
                Program.Instance.Logger.Error(ex, "Error processing monitored item notification");
            }
        }
    }
}
