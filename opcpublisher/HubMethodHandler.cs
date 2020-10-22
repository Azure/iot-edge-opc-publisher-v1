// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Opc.Ua;
using OpcPublisher.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpcPublisher
{
    /// <summary>
    /// Class to handle all IoTHub methods
    /// </summary>
    public class HubMethodHandler
    {
        /// <summary>
        /// Dictionary of available IoTHub direct methods.
        /// </summary>
        public Dictionary<string, MethodCallback> IotHubDirectMethods { get; } = new Dictionary<string, MethodCallback>();

        /// <summary>
        /// Private default constructor
        /// </summary>
        public HubMethodHandler()
        {
            IotHubDirectMethods.Add("PublishNodes", HandlePublishNodesMethodAsync);
            IotHubDirectMethods.Add("UnpublishNodes", HandleUnpublishNodesMethodAsync);
            IotHubDirectMethods.Add("UnpublishAllNodes", HandleUnpublishAllNodesMethodAsync);
            IotHubDirectMethods.Add("GetConfiguredEndpoints", HandleGetConfiguredEndpointsMethodAsync);
            IotHubDirectMethods.Add("GetConfiguredNodesOnEndpoint", HandleGetConfiguredNodesOnEndpointMethodAsync);
            IotHubDirectMethods.Add("GetDiagnosticInfo", HandleGetDiagnosticInfoMethodAsync);
            IotHubDirectMethods.Add("GetDiagnosticLog", HandleGetDiagnosticLogMethodAsync);
            IotHubDirectMethods.Add("GetDiagnosticStartupLog", HandleGetDiagnosticStartupLogMethodAsync);
            IotHubDirectMethods.Add("ExitApplication", HandleExitApplicationMethodAsync);
            IotHubDirectMethods.Add("GetInfo", HandleGetInfoMethodAsync);
        }

        public async void RegisterMethodHandlers(DeviceClient client)
        {
            // init twin properties and method callbacks
            Program.Instance.Logger.Debug($"Register desired properties and method callbacks");

            // register method handlers
            foreach (var iotHubMethod in IotHubDirectMethods)
            {
                await client.SetMethodHandlerAsync(iotHubMethod.Key, iotHubMethod.Value, client).ConfigureAwait(false);
            }
            await client.SetMethodDefaultHandlerAsync(DefaultMethodHandlerAsync, client).ConfigureAwait(false);
        }

        public async void RegisterMethodHandlers(ModuleClient client)
        {
            // init twin properties and method callbacks
            Program.Instance.Logger.Debug($"Register desired properties and method callbacks");

            // register method handlers
            foreach (var iotHubMethod in IotHubDirectMethods)
            {
                await client.SetMethodHandlerAsync(iotHubMethod.Key, iotHubMethod.Value, client).ConfigureAwait(false);
            }
            await client.SetMethodDefaultHandlerAsync(DefaultMethodHandlerAsync, client).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle publish node method call.
        /// </summary>
        public async Task<MethodResponse> HandlePublishNodesMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandlePublishNodesMethodAsync:";
            bool useSecurity = true;
            Uri endpointUri = null;

            OpcAuthenticationMode? desiredAuthenticationMode = null;
            EncryptedNetworkCredential desiredEncryptedCredential = null;

            PublishNodesMethodRequestModel publishNodesMethodData = null;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            HttpStatusCode nodeStatusCode = HttpStatusCode.InternalServerError;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;
            try
            {
                Program.Instance.Logger.Debug($"{logPrefix} called");
                publishNodesMethodData = JsonConvert.DeserializeObject<PublishNodesMethodRequestModel>(methodRequest.DataAsJson);
                endpointUri = new Uri(publishNodesMethodData.EndpointUrl);
                useSecurity = publishNodesMethodData.UseSecurity;

                if (publishNodesMethodData.OpcAuthenticationMode == OpcAuthenticationMode.UsernamePassword)
                {
                    if (string.IsNullOrWhiteSpace(publishNodesMethodData.UserName) && string.IsNullOrWhiteSpace(publishNodesMethodData.Password))
                    {
                        throw new ArgumentException($"If {nameof(publishNodesMethodData.OpcAuthenticationMode)} is set to '{OpcAuthenticationMode.UsernamePassword}', you have to specify '{nameof(publishNodesMethodData.UserName)}' and/or '{nameof(publishNodesMethodData.Password)}'.");
                    }

                    desiredAuthenticationMode = OpcAuthenticationMode.UsernamePassword;
                    desiredEncryptedCredential = await EncryptedNetworkCredential.FromPlainCredential(publishNodesMethodData.UserName, publishNodesMethodData.Password);
                }
            }
            catch (UriFormatException e)
            {
                statusMessage = $"Exception ({e.Message}) while parsing EndpointUrl '{publishNodesMethodData.EndpointUrl}'";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.NotAcceptable;
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                // find/create a session to the endpoint URL and start monitoring the node.
                try
                {
                    // lock the publishing configuration till we are done
                    await Program.Instance._nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);

                    if (Program.Instance.ShutdownTokenSource.IsCancellationRequested)
                    {
                        statusMessage = $"Publisher is in shutdown";
                        Program.Instance.Logger.Warning($"{logPrefix} {statusMessage}");
                        statusResponse.Add(statusMessage);
                        statusCode = HttpStatusCode.Gone;
                    }
                    else
                    {
                        // find the session we need to monitor the node
                        OpcUaSessionManager opcSession = Program.Instance._nodeConfig.OpcSessions.FirstOrDefault(s => s.EndpointUrl.Equals(endpointUri.OriginalString, StringComparison.OrdinalIgnoreCase));

                        // add a new session.
                        if (opcSession == null)
                        {
                            // if the no OpcAuthenticationMode is specified, we create the new session with "Anonymous" auth
                            if (!desiredAuthenticationMode.HasValue)
                            {
                                desiredAuthenticationMode = OpcAuthenticationMode.Anonymous;
                            }

                            // create new session info.
                            opcSession = new OpcUaSessionManager(endpointUri.OriginalString, useSecurity, (uint) Program.Instance._application.ApplicationConfiguration.ClientConfiguration.DefaultSessionTimeout, desiredAuthenticationMode.Value, desiredEncryptedCredential);
                            Program.Instance._nodeConfig.OpcSessions.Add(opcSession);
                            Program.Instance.Logger.Information($"{logPrefix} No matching session found for endpoint '{endpointUri.OriginalString}'. Requested to create a new one.");
                        }
                        else
                        {
                            // a session already exists, so we check, if we need to change authentication settings. This is only true, if the payload contains an OpcAuthenticationMode-Property
                            if (desiredAuthenticationMode.HasValue)
                            {
                                bool reconnectRequired = false;

                                if (opcSession.OpcAuthenticationMode != desiredAuthenticationMode.Value)
                                {
                                    opcSession.OpcAuthenticationMode = desiredAuthenticationMode.Value;
                                    reconnectRequired = true;
                                }

                                if (opcSession.EncryptedAuthCredential != desiredEncryptedCredential)
                                {
                                    opcSession.EncryptedAuthCredential = desiredEncryptedCredential;
                                    reconnectRequired = true;
                                }

                                if (reconnectRequired)
                                {
                                    await opcSession.Reconnect();
                                }
                            }
                        }

                        // process all nodes
                        foreach (var node in publishNodesMethodData.OpcNodes)
                        {
                            // support legacy format
                            if (string.IsNullOrEmpty(node.Id) && !string.IsNullOrEmpty(node.ExpandedNodeId))
                            {
                                node.Id = node.ExpandedNodeId;
                            }

                            NodeId nodeId = null;
                            ExpandedNodeId expandedNodeId = null;
                            bool isNodeIdFormat = true;
                            try
                            {
                                if (node.Id.Contains("nsu=", StringComparison.InvariantCulture))
                                {
                                    expandedNodeId = ExpandedNodeId.Parse(node.Id);
                                    isNodeIdFormat = false;
                                }
                                else
                                {
                                    nodeId = NodeId.Parse(node.Id);
                                    isNodeIdFormat = true;
                                }
                            }
                            catch (Exception e)
                            {
                                statusMessage = $"Exception ({e.Message}) while formatting node '{node.Id}'!";
                                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                                statusResponse.Add(statusMessage);
                                statusCode = HttpStatusCode.NotAcceptable;
                                continue;
                            }

                            try
                            {
                                if (isNodeIdFormat)
                                {
                                    // add the node info to the subscription with the default publishing interval, execute syncronously
                                    Program.Instance.Logger.Debug($"{logPrefix} Request to monitor item with NodeId '{node.Id}' (PublishingInterval: {node.OpcPublishingInterval.ToString() ?? "--"}, SamplingInterval: {node.OpcSamplingInterval.ToString() ?? "--"})");
                                    nodeStatusCode = await opcSession.AddNodeForMonitoringAsync(nodeId, null,
                                        node.OpcPublishingInterval, node.OpcSamplingInterval, node.DisplayName,
                                        node.HeartbeatInterval, node.SkipFirst,
                                        Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false);
                                }
                                else
                                {
                                    // add the node info to the subscription with the default publishing interval, execute syncronously
                                    Program.Instance.Logger.Debug($"{logPrefix} Request to monitor item with ExpandedNodeId '{node.Id}' (PublishingInterval: {node.OpcPublishingInterval.ToString() ?? "--"}, SamplingInterval: {node.OpcSamplingInterval.ToString() ?? "--"})");
                                    nodeStatusCode = await opcSession.AddNodeForMonitoringAsync(null, expandedNodeId,
                                        node.OpcPublishingInterval, node.OpcSamplingInterval, node.DisplayName,
                                        node.HeartbeatInterval, node.SkipFirst,
                                        Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false);
                                }

                                // check and store a result message in case of an error
                                switch (nodeStatusCode)
                                {
                                    case HttpStatusCode.OK:
                                        statusMessage = $"'{node.Id}': already monitored";
                                        Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        break;

                                    case HttpStatusCode.Accepted:
                                        statusMessage = $"'{node.Id}': added";
                                        Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        break;

                                    case HttpStatusCode.Gone:
                                        statusMessage = $"'{node.Id}': session to endpoint does not exist anymore";
                                        Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        statusCode = HttpStatusCode.Gone;
                                        break;

                                    case HttpStatusCode.InternalServerError:
                                        statusMessage = $"'{node.Id}': error while trying to configure";
                                        Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        statusCode = HttpStatusCode.InternalServerError;
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                statusMessage = $"Exception ({e.Message}) while trying to configure publishing node '{node.Id}'";
                                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                                statusResponse.Add(statusMessage);
                                statusCode = HttpStatusCode.InternalServerError;
                            }
                        }
                    }
                }
                catch (AggregateException e)
                {
                    foreach (Exception ex in e.InnerExceptions)
                    {
                        Program.Instance.Logger.Error(ex, $"{logPrefix} Exception");
                    }
                    statusMessage = $"EndpointUrl: '{publishNodesMethodData.EndpointUrl}': exception ({e.Message}) while trying to publish";
                    Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
                catch (Exception e)
                {
                    statusMessage = $"EndpointUrl: '{publishNodesMethodData.EndpointUrl}': exception ({e.Message}) while trying to publish";
                    Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
                finally
                {
                    Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
                }
            }

            // adjust response size
            AdjustResponse(ref statusResponse);

            // build response
            string resultString = JsonConvert.SerializeObject(statusResponse);
            byte[] result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return methodResponse;
        }

        /// <summary>
        /// Handle unpublish node method call.
        /// </summary>
        public async Task<MethodResponse> HandleUnpublishNodesMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleUnpublishNodesMethodAsync:";
            NodeId nodeId = null;
            ExpandedNodeId expandedNodeId = null;
            Uri endpointUri = null;
            bool isNodeIdFormat = true;
            UnpublishNodesMethodRequestModel unpublishNodesMethodData = null;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            HttpStatusCode nodeStatusCode = HttpStatusCode.InternalServerError;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;
            try
            {
                Program.Instance.Logger.Debug($"{logPrefix} called");
                unpublishNodesMethodData = JsonConvert.DeserializeObject<UnpublishNodesMethodRequestModel>(methodRequest.DataAsJson);
                endpointUri = new Uri(unpublishNodesMethodData.EndpointUrl);
            }
            catch (UriFormatException e)
            {
                statusMessage = $"Exception ({e.Message}) while parsing EndpointUrl '{unpublishNodesMethodData.EndpointUrl}'";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                try
                {
                    await Program.Instance._nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                    if (Program.Instance.ShutdownTokenSource.IsCancellationRequested)
                    {
                        statusMessage = $"Publisher is in shutdown";
                        Program.Instance.Logger.Error($"{logPrefix} {statusMessage}");
                        statusResponse.Add(statusMessage);
                        statusCode = HttpStatusCode.Gone;
                    }
                    else
                    {
                        // find the session we need to monitor the node
                        OpcUaSessionManager opcSession = null;
                        try
                        {
                            opcSession = Program.Instance._nodeConfig.OpcSessions.FirstOrDefault(s => s.EndpointUrl.Equals(endpointUri.OriginalString, StringComparison.OrdinalIgnoreCase));
                        }
                        catch
                        {
                            opcSession = null;
                        }

                        if (opcSession == null)
                        {
                            // do nothing if there is no session for this endpoint.
                            statusMessage = $"Session for endpoint '{endpointUri.OriginalString}' not found.";
                            Program.Instance.Logger.Error($"{logPrefix} {statusMessage}");
                            statusResponse.Add(statusMessage);
                            statusCode = HttpStatusCode.Gone;
                        }
                        else
                        {
                            // unpublish all nodes on one endpoint or nodes requested
                            if (unpublishNodesMethodData?.OpcNodes == null || unpublishNodesMethodData.OpcNodes.Count == 0)
                            {
                                // loop through all subscriptions of the session
                                foreach (var subscription in opcSession.OpcSubscriptionManagers)
                                {
                                    // loop through all monitored items
                                    foreach (var monitoredItem in subscription.OpcMonitoredItems)
                                    {
                                        if (monitoredItem.ConfigType == OpcUaMonitoredItemManager.OpcMonitoredItemConfigurationType.NodeId)
                                        {
                                            await opcSession.RequestMonitorItemRemovalAsync(monitoredItem.ConfigNodeId, null, Program.Instance.ShutdownTokenSource.Token, false).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await opcSession.RequestMonitorItemRemovalAsync(null, monitoredItem.ConfigExpandedNodeId, Program.Instance.ShutdownTokenSource.Token, false).ConfigureAwait(false);
                                        }
                                    }
                                }
                                // build response
                                statusMessage = $"All monitored items{(endpointUri != null ? $" on endpoint '{endpointUri.OriginalString}'" : " ")} tagged for removal";
                                statusResponse.Add(statusMessage);
                                Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                            }
                            else
                            {
                                foreach (var node in unpublishNodesMethodData.OpcNodes)
                                {
                                    // support legacy format
                                    if (string.IsNullOrEmpty(node.Id) && !string.IsNullOrEmpty(node.ExpandedNodeId))
                                    {
                                        node.Id = node.ExpandedNodeId;
                                    }

                                    try
                                    {
                                        if (node.Id.Contains("nsu=", StringComparison.InvariantCulture))
                                        {
                                            expandedNodeId = ExpandedNodeId.Parse(node.Id);
                                            isNodeIdFormat = false;
                                        }
                                        else
                                        {
                                            nodeId = NodeId.Parse(node.Id);
                                            isNodeIdFormat = true;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        statusMessage = $"Exception ({e.Message}) while formatting node '{node.Id}'!";
                                        Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        statusCode = HttpStatusCode.NotAcceptable;
                                        continue;
                                    }

                                    try
                                    {
                                        if (isNodeIdFormat)
                                        {
                                            // stop monitoring the node, execute synchronously
                                            Program.Instance.Logger.Information($"{logPrefix} Request to stop monitoring item with NodeId '{nodeId.ToString()}')");
                                            nodeStatusCode = await opcSession.RequestMonitorItemRemovalAsync(nodeId, null, Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            // stop monitoring the node, execute synchronously
                                            Program.Instance.Logger.Information($"{logPrefix} Request to stop monitoring item with ExpandedNodeId '{expandedNodeId.ToString()}')");
                                            nodeStatusCode = await opcSession.RequestMonitorItemRemovalAsync(null, expandedNodeId, Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false);
                                        }

                                        // check and store a result message in case of an error
                                        switch (nodeStatusCode)
                                        {
                                            case HttpStatusCode.OK:
                                                statusMessage = $"Id '{node.Id}': was not configured";
                                                Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                                statusResponse.Add(statusMessage);
                                                break;

                                            case HttpStatusCode.Accepted:
                                                statusMessage = $"Id '{node.Id}': tagged for removal";
                                                Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                                statusResponse.Add(statusMessage);
                                                break;

                                            case HttpStatusCode.Gone:
                                                statusMessage = $"Id '{node.Id}': session to endpoint does not exist anymore";
                                                Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                                statusResponse.Add(statusMessage);
                                                statusCode = HttpStatusCode.Gone;
                                                break;

                                            case HttpStatusCode.InternalServerError:
                                                statusMessage = $"Id '{node.Id}': error while trying to remove";
                                                Program.Instance.Logger.Debug($"{logPrefix} {statusMessage}");
                                                statusResponse.Add(statusMessage);
                                                statusCode = HttpStatusCode.InternalServerError;
                                                break;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        statusMessage = $"Exception ({e.Message}) while trying to tag node '{node.Id}' for removal";
                                        Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                                        statusResponse.Add(statusMessage);
                                        statusCode = HttpStatusCode.InternalServerError;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (AggregateException e)
                {
                    foreach (Exception ex in e.InnerExceptions)
                    {
                        Program.Instance.Logger.Error(ex, $"{logPrefix} Exception");
                    }
                    statusMessage = $"EndpointUrl: '{unpublishNodesMethodData.EndpointUrl}': exception while trying to unpublish";
                    Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
                catch (Exception e)
                {
                    statusMessage = $"EndpointUrl: '{unpublishNodesMethodData.EndpointUrl}': exception ({e.Message}) while trying to unpublish";
                    Program.Instance.Logger.Error($"e, {logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
                finally
                {
                    Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
                }
            }

            // adjust response size
            AdjustResponse(ref statusResponse);

            // build response
            string resultString = JsonConvert.SerializeObject(statusResponse);
            byte[] result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return methodResponse;
        }

        /// <summary>
        /// Handle unpublish all nodes method call.
        /// </summary>
        public async Task<MethodResponse> HandleUnpublishAllNodesMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleUnpublishAllNodesMethodAsync:";
            Uri endpointUri = null;
            UnpublishAllNodesMethodRequestModel unpublishAllNodesMethodData = null;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            try
            {
                Program.Instance.Logger.Debug($"{logPrefix} called");
                if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
                {
                    unpublishAllNodesMethodData = JsonConvert.DeserializeObject<UnpublishAllNodesMethodRequestModel>(methodRequest.DataAsJson);
                }
                if (unpublishAllNodesMethodData != null && unpublishAllNodesMethodData?.EndpointUrl != null)
                {
                    endpointUri = new Uri(unpublishAllNodesMethodData.EndpointUrl);
                }
            }
            catch (UriFormatException e)
            {
                statusMessage = $"Exception ({e.Message}) while parsing EndpointUrl '{unpublishAllNodesMethodData.EndpointUrl}'";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                // schedule to remove all nodes on all sessions
                try
                {
                    await Program.Instance._nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                    if (Program.Instance.ShutdownTokenSource.IsCancellationRequested)
                    {
                        statusMessage = $"Publisher is in shutdown";
                        Program.Instance.Logger.Error($"{logPrefix} {statusMessage}");
                        statusResponse.Add(statusMessage);
                        statusCode = HttpStatusCode.Gone;
                    }
                    else
                    {
                        // loop through all sessions
                        foreach (var session in Program.Instance._nodeConfig.OpcSessions)
                        {
                            bool sessionLocked = false;
                            try
                            {
                                // is an endpoint was given, limit unpublish to this endpoint
                                if (endpointUri != null && !endpointUri.OriginalString.Equals(session.EndpointUrl, StringComparison.InvariantCulture))
                                {
                                    continue;
                                }

                                sessionLocked = await session.LockSessionAsync().ConfigureAwait(false);
                                if (!sessionLocked || Program.Instance.ShutdownTokenSource.IsCancellationRequested)
                                {
                                    break;
                                }

                                // loop through all subscriptions of a connected session
                                foreach (var subscription in session.OpcSubscriptionManagers)
                                {
                                    // loop through all monitored items
                                    foreach (var monitoredItem in subscription.OpcMonitoredItems)
                                    {
                                        if (monitoredItem.ConfigType == OpcUaMonitoredItemManager.OpcMonitoredItemConfigurationType.NodeId)
                                        {
                                            await session.RequestMonitorItemRemovalAsync(monitoredItem.ConfigNodeId, null, Program.Instance.ShutdownTokenSource.Token, false).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await session.RequestMonitorItemRemovalAsync(null, monitoredItem.ConfigExpandedNodeId, Program.Instance.ShutdownTokenSource.Token, false).ConfigureAwait(false);
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                if (sessionLocked)
                                {
                                    session.ReleaseSession();
                                }
                            }
                        }
                        // build response
                        statusMessage = $"All monitored items in all subscriptions{(endpointUri != null ? $" on endpoint '{endpointUri.OriginalString}'" : " ")} tagged for removal";
                        statusResponse.Add(statusMessage);
                        Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                    }
                }
                catch (Exception e)
                {
                    statusMessage = $"EndpointUrl: '{unpublishAllNodesMethodData?.EndpointUrl}': exception ({e.Message}) while trying to unpublish";
                    Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
                finally
                {
                    Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
                }
            }

            // adjust response size to available package size and keep proper json syntax
            byte[] result;
            int maxIndex = statusResponse.Count();
            string resultString = string.Empty;
            while (true)
            {
                resultString = JsonConvert.SerializeObject(statusResponse.GetRange(0, maxIndex));
                result = Encoding.UTF8.GetBytes(resultString);
                if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
                {
                    maxIndex /= 2;
                    continue;
                }
                else
                {
                    break;
                }
            }
            if (maxIndex != statusResponse.Count())
            {
                statusResponse.RemoveRange(maxIndex, statusResponse.Count() - maxIndex);
                statusResponse.Add("Results have been cropped due to package size limitations.");
            }

            // build response
            resultString = JsonConvert.SerializeObject(statusResponse);
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return methodResponse;
        }

        /// <summary>
        /// Handle method call to get all endpoints which published nodes.
        /// </summary>
        public Task<MethodResponse> HandleGetConfiguredEndpointsMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetConfiguredEndpointsMethodAsync:";
            GetConfiguredEndpointsMethodRequestModel getConfiguredEndpointsMethodRequest = null;
            GetConfiguredEndpointsMethodResponseModel getConfiguredEndpointsMethodResponse = new GetConfiguredEndpointsMethodResponseModel();
            uint actualEndpointsCount = 0;
            uint availableEndpointCount = 0;
            uint nodeConfigVersion = 0;
            uint startIndex = 0;
            List<string> endpointUrls = new List<string>();
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            try
            {
                Program.Instance.Logger.Debug($"{logPrefix} called");
                if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
                {
                    getConfiguredEndpointsMethodRequest = JsonConvert.DeserializeObject<GetConfiguredEndpointsMethodRequestModel>(methodRequest.DataAsJson);
                }
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                // get the list of all endpoints
                endpointUrls = Program.Instance._nodeConfig.GetPublisherConfigurationFileEntries(null, false, out nodeConfigVersion).Select(e => e.EndpointUrl.OriginalString).ToList();
                uint endpointsCount = (uint)endpointUrls.Count;

                // validate version
                if (getConfiguredEndpointsMethodRequest?.ContinuationToken != null)
                {
                    uint requestedNodeConfigVersion = (uint)(getConfiguredEndpointsMethodRequest.ContinuationToken >> 32);
                    if (nodeConfigVersion != requestedNodeConfigVersion)
                    {
                        statusMessage = $"The node configuration has changed between calls. Requested version: {requestedNodeConfigVersion:X8}, Current version '{nodeConfigVersion:X8}'";
                        Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                        statusResponse.Add(statusMessage);
                        statusCode = HttpStatusCode.Gone;
                    }
                    startIndex = (uint)(getConfiguredEndpointsMethodRequest.ContinuationToken & 0x0FFFFFFFFL);
                }

                if (statusCode == HttpStatusCode.OK)
                {
                    // set count
                    uint requestedEndpointsCount = endpointsCount - startIndex;
                    availableEndpointCount = endpointsCount - startIndex;
                    actualEndpointsCount = Math.Min(requestedEndpointsCount, availableEndpointCount);

                    // generate response
                    string endpointsString;
                    byte[] endpointsByteArray;
                    while (true)
                    {
                        endpointsString = JsonConvert.SerializeObject(endpointUrls.GetRange((int)startIndex, (int)actualEndpointsCount));
                        endpointsByteArray = Encoding.UTF8.GetBytes(endpointsString);
                        if (endpointsByteArray.Length > SettingsConfiguration.MaxResponsePayloadLength)
                        {
                            actualEndpointsCount /= 2;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                getConfiguredEndpointsMethodResponse.ContinuationToken = null;
                if (actualEndpointsCount < availableEndpointCount)
                {
                    getConfiguredEndpointsMethodResponse.ContinuationToken = ((ulong)nodeConfigVersion << 32) | (actualEndpointsCount + startIndex);
                }
                getConfiguredEndpointsMethodResponse.Endpoints.AddRange(endpointUrls.GetRange((int)startIndex, (int)actualEndpointsCount).Select(e => new ConfiguredEndpointModel(e)).ToList());
                resultString = JsonConvert.SerializeObject(getConfiguredEndpointsMethodResponse);
                result = Encoding.UTF8.GetBytes(resultString);
                Program.Instance.Logger.Information($"{logPrefix} returning {actualEndpointsCount} endpoint(s) (node config version: {nodeConfigVersion:X8})!");
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }

            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return Task.FromResult(methodResponse);
        }

        /// <summary>
        /// Handle method call to get list of configured nodes on a specific endpoint.
        /// </summary>
        public Task<MethodResponse> HandleGetConfiguredNodesOnEndpointMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetConfiguredNodesOnEndpointMethodAsync:";
            Uri endpointUri = null;
            GetConfiguredNodesOnEndpointMethodRequestModel getConfiguredNodesOnEndpointMethodRequest = null;
            uint nodeConfigVersion = 0;
            GetConfiguredNodesOnEndpointMethodResponseModel getConfiguredNodesOnEndpointMethodResponse = new GetConfiguredNodesOnEndpointMethodResponseModel();
            uint actualNodeCount = 0;
            uint availableNodeCount = 0;
            uint requestedNodeCount = 0;
            List<OpcNodeOnEndpointModel> opcNodes = new List<OpcNodeOnEndpointModel>();
            uint startIndex = 0;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            try
            {
                Program.Instance.Logger.Debug($"{logPrefix} called");
                getConfiguredNodesOnEndpointMethodRequest = JsonConvert.DeserializeObject<GetConfiguredNodesOnEndpointMethodRequestModel>(methodRequest.DataAsJson);
                endpointUri = new Uri(getConfiguredNodesOnEndpointMethodRequest.EndpointUrl);
            }
            catch (UriFormatException e)
            {
                statusMessage = $"Exception ({e.Message}) while parsing EndpointUrl '{getConfiguredNodesOnEndpointMethodRequest.EndpointUrl}'";
                Program.Instance.Logger.Error(e, $"{logPrefix} {statusMessage}");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                // get the list of published nodes for the endpoint
                List<ConfigurationFileEntryModel> configFileEntries = Program.Instance._nodeConfig.GetPublisherConfigurationFileEntries(endpointUri.OriginalString, false, out nodeConfigVersion);

                // return if there are no nodes configured for this endpoint
                if (configFileEntries.Count == 0)
                {
                    statusMessage = $"There are no nodes configured for endpoint '{endpointUri.OriginalString}'";
                    Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.OK;
                }
                else
                {
                    foreach (var configFileEntry in configFileEntries)
                    {
                        opcNodes.AddRange(configFileEntry.OpcNodes);
                    }
                    uint configuredNodesOnEndpointCount = (uint)opcNodes.Count();

                    // validate version
                    startIndex = 0;
                    if (getConfiguredNodesOnEndpointMethodRequest?.ContinuationToken != null)
                    {
                        uint requestedNodeConfigVersion = (uint)(getConfiguredNodesOnEndpointMethodRequest.ContinuationToken >> 32);
                        if (nodeConfigVersion != requestedNodeConfigVersion)
                        {
                            statusMessage = $"The node configuration has changed between calls. Requested version: {requestedNodeConfigVersion:X8}, Current version '{nodeConfigVersion:X8}'!";
                            Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                            statusResponse.Add(statusMessage);
                            statusCode = HttpStatusCode.Gone;
                        }
                        startIndex = (uint)(getConfiguredNodesOnEndpointMethodRequest.ContinuationToken & 0x0FFFFFFFFL);
                    }

                    if (statusCode == HttpStatusCode.OK)
                    {
                        // set count
                        requestedNodeCount = configuredNodesOnEndpointCount - startIndex;
                        availableNodeCount = configuredNodesOnEndpointCount - startIndex;
                        actualNodeCount = Math.Min(requestedNodeCount, availableNodeCount);

                        // generate response
                        string publishedNodesString;
                        byte[] publishedNodesByteArray;
                        while (true)
                        {
                            publishedNodesString = JsonConvert.SerializeObject(opcNodes.GetRange((int)startIndex, (int)actualNodeCount));
                            publishedNodesByteArray = Encoding.UTF8.GetBytes(publishedNodesString);
                            if (publishedNodesByteArray.Length > SettingsConfiguration.MaxResponsePayloadLength)
                            {
                                actualNodeCount /= 2;
                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                getConfiguredNodesOnEndpointMethodResponse.ContinuationToken = null;
                if (actualNodeCount < availableNodeCount)
                {
                    getConfiguredNodesOnEndpointMethodResponse.ContinuationToken = ((ulong)nodeConfigVersion << 32) | (actualNodeCount + startIndex);
                }
                getConfiguredNodesOnEndpointMethodResponse.OpcNodes.AddRange(opcNodes.GetRange((int)startIndex, (int)actualNodeCount).Select(n => new OpcNodeOnEndpointModel(n.Id)
                {
                    OpcPublishingInterval = n.OpcPublishingInterval,
                    OpcSamplingInterval = n.OpcSamplingInterval,
                    DisplayName = n.DisplayName
                }).ToList());
                getConfiguredNodesOnEndpointMethodResponse.EndpointUrl = endpointUri.OriginalString;
                resultString = JsonConvert.SerializeObject(getConfiguredNodesOnEndpointMethodResponse);
                Program.Instance.Logger.Information($"{logPrefix} Success returning {actualNodeCount} node(s) of {availableNodeCount} (start: {startIndex}) (node config version: {nodeConfigVersion:X8})!");
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return Task.FromResult(methodResponse);
        }

        /// <summary>
        /// Handle method call to get diagnostic information.
        /// </summary>
        public Task<MethodResponse> HandleGetDiagnosticInfoMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetDiagnosticInfoMethodAsync:";
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            // get the diagnostic info
            DiagnosticInfoMethodResponseModel diagnosticInfo = new DiagnosticInfoMethodResponseModel();
            try
            {
                diagnosticInfo = Program.Instance._diag.GetDiagnosticInfo();
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while reading diagnostic info";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                resultString = JsonConvert.SerializeObject(diagnosticInfo);
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return Task.FromResult(methodResponse);
        }

        /// <summary>
        /// Handle method call to get log information.
        /// </summary>
        public async Task<MethodResponse> HandleGetDiagnosticLogMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetDiagnosticLogMethodAsync:";
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            // get the diagnostic info
            DiagnosticLogMethodResponseModel diagnosticLogMethodResponseModel = new DiagnosticLogMethodResponseModel();
            try
            {
                diagnosticLogMethodResponseModel = await Program.Instance._diag.GetDiagnosticLogAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while reading diagnostic log";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                resultString = JsonConvert.SerializeObject(diagnosticLogMethodResponseModel);
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return methodResponse;
        }

        /// <summary>
        /// Handle method call to get log information.
        /// </summary>
        public async Task<MethodResponse> HandleGetDiagnosticStartupLogMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetDiagnosticStartupLogMethodAsync:";
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            // get the diagnostic info
            DiagnosticLogMethodResponseModel diagnosticLogMethodResponseModel = new DiagnosticLogMethodResponseModel();
            try
            {
                diagnosticLogMethodResponseModel = await Program.Instance._diag.GetDiagnosticStartupLogAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while reading diagnostic startup log";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                resultString = JsonConvert.SerializeObject(diagnosticLogMethodResponseModel);
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return methodResponse;
        }

        /// <summary>
        /// Handle method call to get log information.
        /// </summary>
        public Task<MethodResponse> HandleExitApplicationMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleExitApplicationMethodAsync:";
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            ExitApplicationMethodRequestModel exitApplicationMethodRequest = null;
            try
            {
                if (!string.IsNullOrEmpty(methodRequest.DataAsJson))
                {
                    exitApplicationMethodRequest = JsonConvert.DeserializeObject<ExitApplicationMethodRequestModel>(methodRequest.DataAsJson);
                }
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while deserializing message payload";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            if (statusCode == HttpStatusCode.OK)
            {
                // get the parameter
                ExitApplicationMethodRequestModel exitApplication = new ExitApplicationMethodRequestModel();
                try
                {
                    int secondsTillExit = exitApplicationMethodRequest != null ? exitApplicationMethodRequest.SecondsTillExit : 5;
                    secondsTillExit = secondsTillExit < 5 ? 5 : secondsTillExit;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() => ExitApplicationAsync(secondsTillExit).ConfigureAwait(false));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    statusMessage = $"Module will exit now...";
                    Program.Instance.Logger.Information($"{logPrefix} {statusMessage}");
                    statusResponse.Add(statusMessage);
                }
                catch (Exception e)
                {
                    statusMessage = $"Exception ({e.Message}) while scheduling application exit";
                    Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                    statusResponse.Add(statusMessage);
                    statusCode = HttpStatusCode.InternalServerError;
                }
            }

            // build response
            byte[] result = null;
            string resultString = null;
            resultString = JsonConvert.SerializeObject(statusResponse);
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return Task.FromResult(methodResponse);
        }

        /// <summary>
        /// Handle method call to get application information.
        /// </summary>
        public Task<MethodResponse> HandleGetInfoMethodAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "HandleGetInfoMethodAsync:";
            GetInfoMethodResponseModel getInfoMethodResponseModel = new GetInfoMethodResponseModel();
            HttpStatusCode statusCode = HttpStatusCode.OK;
            List<string> statusResponse = new List<string>();
            string statusMessage = string.Empty;

            try
            {
                // get the info
                getInfoMethodResponseModel.VersionMajor = Assembly.GetExecutingAssembly().GetName().Version.Major;
                getInfoMethodResponseModel.VersionMinor = Assembly.GetExecutingAssembly().GetName().Version.Minor;
                getInfoMethodResponseModel.VersionPatch = Assembly.GetExecutingAssembly().GetName().Version.Build;
                getInfoMethodResponseModel.SemanticVersion = (Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute).InformationalVersion;
                getInfoMethodResponseModel.InformationalVersion = (Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute).InformationalVersion;
                getInfoMethodResponseModel.OS = RuntimeInformation.OSDescription;
                getInfoMethodResponseModel.OSArchitecture = RuntimeInformation.OSArchitecture;
                getInfoMethodResponseModel.FrameworkDescription = RuntimeInformation.FrameworkDescription;
            }
            catch (Exception e)
            {
                statusMessage = $"Exception ({e.Message}) while retrieving info";
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception");
                statusResponse.Add(statusMessage);
                statusCode = HttpStatusCode.InternalServerError;
            }

            // build response
            byte[] result = null;
            string resultString = null;
            if (statusCode == HttpStatusCode.OK)
            {
                resultString = JsonConvert.SerializeObject(getInfoMethodResponseModel);
            }
            else
            {
                resultString = JsonConvert.SerializeObject(statusResponse);
            }
            result = Encoding.UTF8.GetBytes(resultString);
            if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
            {
                Program.Instance.Logger.Error($"{logPrefix} Response size is too long");
                Array.Resize(ref result, result.Length > SettingsConfiguration.MaxResponsePayloadLength ? SettingsConfiguration.MaxResponsePayloadLength : result.Length);
            }
            MethodResponse methodResponse = new MethodResponse(result, (int)statusCode);
            Program.Instance.Logger.Information($"{logPrefix} completed with result {statusCode.ToString()}");
            return Task.FromResult(methodResponse);
        }

        /// <summary>
        /// Method that is called for any unimplemented call. Just returns that info to the caller
        /// </summary>
        public Task<MethodResponse> DefaultMethodHandlerAsync(MethodRequest methodRequest, object userContext)
        {
            string logPrefix = "DefaultMethodHandlerAsync:";
            string errorMessage = $"Method '{methodRequest.Name}' successfully received, but this method is not implemented";
            Program.Instance.Logger.Information($"{logPrefix} {errorMessage}");

            string resultString = JsonConvert.SerializeObject(errorMessage);
            byte[] result = Encoding.UTF8.GetBytes(resultString);
            MethodResponse methodResponse = new MethodResponse(result, (int)HttpStatusCode.NotImplemented);
            return Task.FromResult(methodResponse);
        }
    
        /// <summary>
        /// Adjust the method response to the max payload size.
        /// </summary>
        private void AdjustResponse(ref List<string> statusResponse)
        {
            byte[] result;
            int maxIndex = statusResponse.Count();
            string resultString = string.Empty;
            while (true)
            {
                resultString = JsonConvert.SerializeObject(statusResponse.GetRange(0, maxIndex));
                result = Encoding.UTF8.GetBytes(resultString);
                if (result.Length > SettingsConfiguration.MaxResponsePayloadLength)
                {
                    maxIndex /= 2;
                    continue;
                }
                else
                {
                    break;
                }
            }
            if (maxIndex != statusResponse.Count())
            {
                statusResponse.RemoveRange(maxIndex, statusResponse.Count() - maxIndex);
                statusResponse.Add("Results have been cropped due to package size limitations.");
            }
        }

        /// <summary>
        /// Exit the application.
        /// </summary>
        public async Task ExitApplicationAsync(int secondsTillExit)
        {
            string logPrefix = "ExitApplicationAsync:";

            // sanity check parameter
            if (secondsTillExit <= 0)
            {
                Program.Instance.Logger.Information($"{logPrefix} Time to exit adjusted to {secondsTillExit} seconds...");
                secondsTillExit = 5;
            }

            // wait and exit
            while (secondsTillExit > 0)
            {
                Program.Instance.Logger.Information($"{logPrefix} Exiting in {secondsTillExit} seconds...");
                secondsTillExit--;
                await Task.Delay(1000).ConfigureAwait(false);
            }

            // exit
            Environment.Exit(2);
        }
    }
}