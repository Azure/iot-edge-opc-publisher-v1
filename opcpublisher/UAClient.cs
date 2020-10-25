// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpcPublisher
{
    class UAClient
    {
        /// <summary>
        /// Number of configured OPC UA sessions.
        /// </summary>
        public int NumberOfOpcSessionsConfigured
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Count();
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Number of connected OPC UA session.
        /// </summary>
        public int NumberOfOpcSessionsConnected
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    result = OpcSessions.Count(s => s.State == OpcUaSessionWrapper.SessionState.Connected);
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Number of configured OPC UA subscriptions.
        /// </summary>
        public int NumberOfOpcSubscriptionsConfigured
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    foreach (var opcSession in OpcSessions)
                    {

                        result += opcSession.GetNumberOfOpcSubscriptions();
                    }
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }
        /// <summary>
        /// Number of connected OPC UA subscriptions.
        /// </summary>
        public int NumberOfOpcSubscriptionsConnected
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    var opcSessions = OpcSessions.Where(s => s.State == OpcUaSessionWrapper.SessionState.Connected);
                    foreach (var opcSession in opcSessions)
                    {
                        result += opcSession.GetNumberOfOpcSubscriptions();
                    }
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Number of OPC UA nodes configured to monitor.
        /// </summary>
        public int NumberOfOpcMonitoredItemsConfigured
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    foreach (var opcSession in OpcSessions)
                    {
                        result += opcSession.GetNumberOfOpcMonitoredItemsConfigured();
                    }
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Number of monitored OPC UA nodes.
        /// </summary>
        public int NumberOfOpcMonitoredItemsMonitored
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    var opcSessions = OpcSessions.Where(s => s.State == OpcUaSessionWrapper.SessionState.Connected);
                    foreach (var opcSession in opcSessions)
                    {
                        result += opcSession.GetNumberOfOpcMonitoredItemsMonitored();
                    }
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Number of OPC UA nodes requested to stop monitoring.
        /// </summary>
        public int NumberOfOpcMonitoredItemsToRemove
        {
            get
            {
                int result = 0;
                try
                {
                    OpcSessionsListSemaphore.Wait();
                    foreach (var opcSession in OpcSessions)
                    {
                        result += opcSession.GetNumberOfOpcMonitoredItemsToRemove();
                    }
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
                return result;
            }
        }

        /// <summary>
        /// Semaphore to protect the node configuration data structures.
        /// </summary>
        public SemaphoreSlim PublisherNodeConfigurationSemaphore { get; set; }

        /// <summary>
        /// Semaphore to protect the node configuration file.
        /// </summary>
        public SemaphoreSlim PublisherNodeConfigurationFileSemaphore { get; set; }

        /// <summary>
        /// Semaphore to protect the OPC UA sessions list.
        /// </summary>
        public SemaphoreSlim OpcSessionsListSemaphore { get; set; }

        /// <summary>
        /// List of configured OPC UA sessions.
        /// </summary>
        public List<OpcUaSessionWrapper> OpcSessions { get; set; } = new List<OpcUaSessionWrapper>();

        static public void PublishNode(NodePublishingConfigurationModel node)
        {
            // lock the publishing configuration till we are done
            await OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);

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
                OpcUaSessionWrapper opcSession = Program.Instance._nodeConfig.OpcSessions.FirstOrDefault(s => s.EndpointUrl.Equals(endpointUri.OriginalString, StringComparison.OrdinalIgnoreCase));

                // add a new session.
                if (opcSession == null)
                {
                    // if the no OpcAuthenticationMode is specified, we create the new session with "Anonymous" auth
                    if (!desiredAuthenticationMode.HasValue)
                    {
                        desiredAuthenticationMode = OpcUserSessionAuthenticationMode.Anonymous;
                    }

                    // create new session info.
                    opcSession = new OpcUaSessionWrapper(endpointUri.OriginalString, useSecurity, (uint)Program.Instance._application.ApplicationConfiguration.ClientConfiguration.DefaultSessionTimeout, desiredAuthenticationMode.Value, desiredEncryptedCredential);
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

            Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
        }

        static public void UnpublishNode(NodePublishingConfigurationModel node)
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
                OpcUaSessionWrapper opcSession = null;
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
                        foreach (var subscription in opcSession.OpcSubscriptionWrappers)
                        {
                            // loop through all monitored items
                            foreach (var monitoredItem in subscription.OpcMonitoredItems)
                            {
                                if (monitoredItem.ConfigType == OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.NodeId)
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

            Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
        }

        static public List<ConfigurationFileEntryModel> GetListofPublishedNodes()
        {
            List<ConfigurationFileEntryModel> publisherConfigurationFileEntries = new List<ConfigurationFileEntryModel>();
            nodeConfigVersion = (uint)OpcUaSessionWrapper.NodeConfigVersion;
            try
            {
                PublisherNodeConfigurationSemaphore.Wait();

                try
                {
                    OpcSessionsListSemaphore.Wait();

                    // itereate through all sessions, subscriptions and monitored items and create config file entries
                    foreach (var session in OpcSessions)
                    {
                        bool sessionLocked = false;
                        try
                        {
                            sessionLocked = session.LockSessionAsync().Result;
                            if (sessionLocked && (endpointUrl == null || session.EndpointUrl.Equals(endpointUrl, StringComparison.OrdinalIgnoreCase)))
                            {
                                ConfigurationFileEntryModel publisherConfigurationFileEntry = new ConfigurationFileEntryModel();

                                publisherConfigurationFileEntry.EndpointUrl = new Uri(session.EndpointUrl);
                                publisherConfigurationFileEntry.OpcAuthenticationMode = session.OpcAuthenticationMode;
                                publisherConfigurationFileEntry.EncryptedAuthCredential = session.EncryptedAuthCredential;
                                publisherConfigurationFileEntry.UseSecurity = session.UseSecurity;
                                publisherConfigurationFileEntry.OpcNodes = new List<OpcNodeOnEndpointModel>();

                                foreach (var subscription in session.OpcSubscriptionWrappers)
                                {
                                    foreach (var monitoredItem in subscription.OpcMonitoredItems)
                                    {
                                        // ignore items tagged to stop
                                        if (monitoredItem.State != OpcUaMonitoredItemWrapper.OpcMonitoredItemState.RemovalRequested || getAll == true)
                                        {
                                            OpcNodeOnEndpointModel opcNodeOnEndpoint = new OpcNodeOnEndpointModel(monitoredItem.OriginalId) {
                                                OpcPublishingInterval = subscription.PublishingInterval,
                                                OpcSamplingInterval = monitoredItem.RequestedSamplingIntervalFromConfiguration ? monitoredItem.RequestedSamplingInterval : (int?)null,
                                                DisplayName = monitoredItem.DisplayNameFromConfiguration ? monitoredItem.DisplayName : null,
                                                HeartbeatInterval = monitoredItem.HeartbeatIntervalFromConfiguration ? (int?)monitoredItem.HeartbeatInterval : null,
                                                SkipFirst = monitoredItem.SkipFirstFromConfiguration ? (bool?)monitoredItem.SkipFirst : null
                                            };
                                            publisherConfigurationFileEntry.OpcNodes.Add(opcNodeOnEndpoint);
                                        }
                                    }
                                }
                                publisherConfigurationFileEntries.Add(publisherConfigurationFileEntry);
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
                    nodeConfigVersion = (uint)OpcUaSessionWrapper.NodeConfigVersion;
                }
                finally
                {
                    OpcSessionsListSemaphore.Release();
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Reading configuration file entries failed.");
                publisherConfigurationFileEntries = null;
            }
            finally
            {
                PublisherNodeConfigurationSemaphore.Release();
            }
            return publisherConfigurationFileEntries;
        }

        public void Close()
        {
            while (OpcSessions.Count > 0)
            {
                OpcSessionsListSemaphore.Wait();
                OpcUaSessionWrapper opcSession = OpcSessions.ElementAt(0);
                opcSession?.ShutdownAsync().Wait();
                OpcSessions.RemoveAt(0);
                OpcSessionsListSemaphore.Release();
            }
        }

        private UAClient()
        {
            // do nothing
        }

        private static UAClient _instance = new UAClient();


    }
}
