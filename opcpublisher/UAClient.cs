// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using Opc.Ua.Client;
using OpcPublisher.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OpcPublisher
{
    /// <summary>
    /// Enum that defines the authentication method to connect to OPC UA
    /// </summary>
    public enum OpcUserSessionAuthenticationMode
    {
        /// <summary>
        /// Anonymous authentication
        /// </summary>
        Anonymous,
        /// <summary>
        /// Username/Password authentication
        /// </summary>
        UsernamePassword
    }

    /// <summary>
    /// The state of the session object.
    /// </summary>
    public enum SessionState
    {
        Disconnected = 0,
        Connecting,
        Connected,
    }

    class UAClient
    {
        /// <summary>
        /// The version of the node configuration. Each change in the configuration
        /// increments the version to protect get calls using continuation tokens.
        /// </summary>
        public static int NodeConfigVersion;

        /// <summary>
        /// The endpoint to connect to for the session.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// The OPC UA stack session object of the session.
        /// </summary>
        public Session OpcUaClientSession { get; set; }

        /// <summary>
        /// The state of the session.
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Counts session connection attempts which were unsuccessful.
        /// </summary>
        public uint UnsuccessfulConnectionCount { get; set; }

        /// <summary>
        /// Counts missed keep alives.
        /// </summary>
        public uint MissedKeepAlives { get; set; }

        /// <summary>
        /// The default publishing interval to use on this session.
        /// </summary>
        public int PublishingInterval { get; set; }

        /// <summary>
        /// The OPC UA timeout setting to use for the OPC UA session.
        /// </summary>
        public uint SessionTimeout { get; }

        /// <summary>
        /// Flag to control if a secure or unsecure OPC UA transport should be used for the session.
        /// </summary>
        public bool UseSecurity { get; set; } = true;

        /// <summary>
        /// Signals to run the connect and monitor task.
        /// </summary>
        public AutoResetEvent ConnectAndMonitorSession { get; set; }

        /// <summary>
        /// The encrypted credential for authentication against the OPC UA Server. This is only used, when <see cref="OpcAuthenticationMode"/> is set to "UsernamePassword".
        /// </summary>
        public EncryptedNetworkCredential EncryptedAuthCredential { get; set; }

        /// <summary>
        /// The authentication mode to use for authentication against the OPC UA Server.
        /// </summary>
        public OpcUserSessionAuthenticationMode OpcAuthenticationMode { get; set; }
        
        /// <summary>
        /// Number of configured OPC UA sessions.
        /// </summary>
        public int NumberOfOpcSessionsConfigured
        {
            get
            {
                int result = 0;
               
                //TODO

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
                
                //TODO

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
              
                //TODO

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
                
                //TODO
                
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
                
                //TODO

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
                
                //TODO

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
              
                //TODO

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
        /// Number of subscirptoins on this session.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOpcSubscriptions()
        {
            int result = 0;
            bool sessionLocked = false;
            
            //TODO

            return result;
        }

        /// <summary>
        /// Number of configured monitored items on this session.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOpcMonitoredItemsConfigured()
        {
            int result = 0;
            bool sessionLocked = false;
           
            //TODO

            return result;
        }

        /// <summary>
        /// Number of actually monitored items on this sessions.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOpcMonitoredItemsMonitored()
        {
            int result = 0;
            bool sessionLocked = false;
            
            //TODO

            return result;
        }

        /// <summary>
        /// Number of monitored items to be removed from this session.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfOpcMonitoredItemsToRemove()
        {
            int result = 0;
            bool sessionLocked = false;
           
            //TODO

            return result;
        }

        public async Task Reconnect()
        {
            try
            {
                var sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                if (sessionLocked)
                {
                    InternalDisconnect();
                }

                if (State != SessionState.Disconnected)
                {
                    throw new Exception("Could not disconnect session.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error while disconnecting session", ex);
            }
            finally
            {
                ReleaseSession();
            }
        }

        /// <summary>
        /// This task is started when a session is configured and is running till session shutdown and ensures:
        /// - disconnected sessions are reconnected.
        /// - monitored nodes are no longer monitored if requested to do so.
        /// - monitoring for a node starts if it is required.
        /// - unused subscriptions (without any nodes to monitor) are removed.
        /// - sessions with out subscriptions are removed.
        /// </summary>
        public async Task ConnectAndMonitorAsync()
        {
            WaitHandle[] connectAndMonitorEvents = new WaitHandle[]
            {
                _sessionCancelationToken.WaitHandle,
                ConnectAndMonitorSession
            };

            // run till session is closed
            while (true)
            {
                try
                {
                    // wait till:
                    // - cancellation is requested
                    // - got signaled because we need to check for pending session activity
                    // - timeout to try to reestablish any disconnected sessions
                    try
                    {
                        WaitHandle.WaitAny(connectAndMonitorEvents, SettingsConfiguration.SessionConnectWaitSec * 1000);
                        _sessionCancelationToken.ThrowIfCancellationRequested();
                    }
                    catch
                    {
                        break;
                    }

                    await ConnectSessionAsync(_sessionCancelationToken).ConfigureAwait(false);

                    await MonitorNodesAsync(_sessionCancelationToken).ConfigureAwait(false);
                    _sessionCancelationToken.ThrowIfCancellationRequested();

                    await StopMonitoringNodesAsync(_sessionCancelationToken).ConfigureAwait(false);
                    _sessionCancelationToken.ThrowIfCancellationRequested();

                    await RemoveUnusedSubscriptionsAsync(_sessionCancelationToken).ConfigureAwait(false);
                    _sessionCancelationToken.ThrowIfCancellationRequested();

                    await RemoveUnusedSessionsAsync(_sessionCancelationToken).ConfigureAwait(false);
                    _sessionCancelationToken.ThrowIfCancellationRequested();
                }
                catch (Exception e)
                {
                    if (!_sessionCancelationToken.IsCancellationRequested)
                    {
                        Program.Instance.Logger.Error(e, "Exception");
                    }
                    else
                    {
                        break;
                    }
                }
                finally
                {
                    // update the config file if required
                    await Program.Instance._nodeConfig.UpdateNodeConfigurationFileAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Connects the session if it is disconnected.
        /// </summary>
        public async Task ConnectSessionAsync(CancellationToken ct)
        {
            bool sessionLocked = false;
            try
            {
                EndpointDescription selectedEndpoint = null;
                ConfiguredEndpoint configuredEndpoint = null;

                try
                {
                    sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                    // if the session is already connected or connecting or shutdown in progress, return
                    if (!sessionLocked || ct.IsCancellationRequested || State == SessionState.Connected || State == SessionState.Connecting)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    throw;
                }

                Program.Instance.Logger.Information($"Connect and monitor session and nodes on endpoint '{EndpointUrl}'.");
                State = SessionState.Connecting;
                try
                {
                    // release the session to not block for high network timeouts.
                    ReleaseSession();
                    sessionLocked = false;

                    // start connecting
                    selectedEndpoint = CoreClientUtils.SelectEndpoint(EndpointUrl, UseSecurity);
                    configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(Program.Instance._application.ApplicationConfiguration));
                    uint timeout = SessionTimeout * ((UnsuccessfulConnectionCount >= SettingsConfiguration.OpcSessionCreationBackoffMax) ? SettingsConfiguration.OpcSessionCreationBackoffMax : UnsuccessfulConnectionCount + 1);
                    Program.Instance.Logger.Information($"Create {(UseSecurity ? "secured" : "unsecured")} session for endpoint URI '{EndpointUrl}' with timeout of {timeout} ms.");

                    UserIdentity userIdentity = null;

                    switch (OpcAuthenticationMode)
                    {
                        case OpcUserSessionAuthenticationMode.Anonymous:
                            userIdentity = new UserIdentity(new AnonymousIdentityToken());
                            break;
                        case OpcUserSessionAuthenticationMode.UsernamePassword:
                            if (EncryptedAuthCredential == null)
                            {
                                throw new NullReferenceException("Please specity user credential to authentication with mode 'UsernamePassword'");
                            }

                            var plainCredential = await EncryptedAuthCredential.Decrypt();

                            userIdentity = new UserIdentity(plainCredential.UserName, plainCredential.Password);
                            break;
                        default:
                            throw new NotImplementedException($"The authentication mode '{OpcAuthenticationMode}' has not yet been implemented.");
                    }

                    OpcUaClientSession = Session.Create(
                            Program.Instance._application.ApplicationConfiguration,
                            configuredEndpoint,
                            true,
                            false,
                            Program.Instance._application.ApplicationConfiguration.ApplicationName,
                            timeout,
                            userIdentity,
                            null).Result;
                }
                catch (Exception e)
                {
                    Program.Instance.Logger.Error(e, $"Session creation to endpoint '{EndpointUrl}' failed {++UnsuccessfulConnectionCount} time(s). Please verify if server is up and Publisher configuration is correct.");
                    State = SessionState.Disconnected;
                    OpcUaClientSession = null;
                    return;
                }
                finally
                {
                    if (OpcUaClientSession != null)
                    {
                        sessionLocked = await LockSessionAsync().ConfigureAwait(false);
                        if (sessionLocked)
                        {
                            Program.Instance.Logger.Information($"Session successfully created with Id {OpcUaClientSession.SessionId}.");
                            if (!selectedEndpoint.EndpointUrl.Equals(configuredEndpoint.EndpointUrl.OriginalString, StringComparison.OrdinalIgnoreCase))
                            {
                                Program.Instance.Logger.Information($"the Server has updated the EndpointUrl to '{selectedEndpoint.EndpointUrl}'");
                            }

                            // init object state and install keep alive
                            UnsuccessfulConnectionCount = 0;
                            OpcUaClientSession.KeepAlive += StandardClient_KeepAlive;

                            // fetch the namespace array and cache it. it will not change as long the session exists.
                            DataValue namespaceArrayNodeValue = OpcUaClientSession.ReadValue(VariableIds.Server_NamespaceArray);
                            _namespaceTable.Update(namespaceArrayNodeValue.GetValue<string[]>(null));

                            // show the available namespaces
                            Program.Instance.Logger.Information($"The session to endpoint '{selectedEndpoint.EndpointUrl}' has {_namespaceTable.Count} entries in its namespace array:");
                            int i = 0;
                            foreach (var ns in _namespaceTable.ToArray())
                            {
                                Program.Instance.Logger.Information($"Namespace index {i++}: {ns}");
                            }
                            State = SessionState.Connected;
                        }
                        else
                        {
                            State = SessionState.Disconnected;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
        }

        public static void RemoveAllMonitoredNodes()
        {
            // schedule to remove all nodes on all sessions
            try
            {
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
                    foreach (var session in UAClient.OpcSessions)
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
                            foreach (var subscription in session.OpcSubscriptionWrappers)
                            {
                                // loop through all monitored items
                                foreach (var monitoredItem in subscription.OpcMonitoredItems)
                                {
                                    if (monitoredItem.ConfigType == OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.NodeId)
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

        /// <summary>
        /// Monitoring for a node starts if it is required.
        /// </summary>
        public async Task MonitorNodesAsync(CancellationToken ct)
        {
            bool sessionLocked = false;
            try
            {
                try
                {
                    sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                    // if the session is not connected or shutdown in progress, return
                    if (!sessionLocked || ct.IsCancellationRequested || State != SessionState.Connected)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    throw;
                }

                // ensure all nodes in all subscriptions of this session are monitored.
                foreach (var opcSubscription in OpcSubscriptionWrappers)
                {
                    // create the subscription, if it is not yet there.
                    if (opcSubscription.OpcUaClientSubscription == null)
                    {
                        opcSubscription.OpcUaClientSubscription = CreateSubscription(opcSubscription.PublishingInterval, out int revisedPublishingInterval);
                        Program.Instance.Logger.Information($"Create subscription on endpoint '{EndpointUrl}' requested OPC publishing interval is {opcSubscription.PublishingInterval} ms. (revised: {revisedPublishingInterval} ms)");
                        opcSubscription.PublishingInterval = revisedPublishingInterval;
                    }

                    // process all unmonitored items.
                    var unmonitoredItems = opcSubscription.OpcMonitoredItems.Where(i => (i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Unmonitored || i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.UnmonitoredNamespaceUpdateRequested)).ToArray();
                    int monitoredItemsCount = 0;
                    bool haveUnmonitoredItems = false;
                    if (unmonitoredItems.Any())
                    {
                        haveUnmonitoredItems = true;
                        monitoredItemsCount = opcSubscription.OpcMonitoredItems.Count(i => (i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Monitored));
                        Program.Instance.Logger.Information($"Start monitoring items on endpoint '{EndpointUrl}'. Currently monitoring {monitoredItemsCount} items.");
                    }

                    // init perf data
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    for (int index = 0; index < unmonitoredItems.Length; index++)
                    {
                        var item = unmonitoredItems[index];

                        // if the session is not connected or a shutdown is in progress, we stop trying and wait for the next cycle
                        if (ct.IsCancellationRequested || State != SessionState.Connected)
                        {
                            break;
                        }

                        NodeId currentNodeId = null;
                        try
                        {
                            // update the namespace of the node if requested. there are two cases where this is requested:
                            // 1) publishing requests via the OPC server method are raised using a NodeId format. for those
                            //    the NodeId format is converted into an ExpandedNodeId format
                            // 2) ExpandedNodeId configuration file entries do not have at parsing time a session to get
                            //    the namespace index. this is set now.
                            if (item.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.UnmonitoredNamespaceUpdateRequested)
                            {
                                if (item.ConfigType == OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.ExpandedNodeId)
                                {
                                    int namespaceIndex = _namespaceTable.GetIndex(item.ConfigExpandedNodeId?.NamespaceUri);
                                    if (namespaceIndex < 0)
                                    {
                                        Program.Instance.Logger.Information($"The namespace URI of node '{item.ConfigExpandedNodeId.ToString()}' can be not mapped to a namespace index.");
                                    }
                                    else
                                    {
                                        item.ConfigExpandedNodeId = new ExpandedNodeId(item.ConfigExpandedNodeId.Identifier, (ushort)namespaceIndex, item.ConfigExpandedNodeId?.NamespaceUri, 0);
                                    }
                                }
                                if (item.ConfigType == OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.NodeId)
                                {
                                    string namespaceUri = _namespaceTable.ToArray().ElementAtOrDefault(item.ConfigNodeId.NamespaceIndex);
                                    if (string.IsNullOrEmpty(namespaceUri))
                                    {
                                        Program.Instance.Logger.Information($"The namespace index of node '{item.ConfigNodeId.ToString()}' is invalid and the node format can not be updated.");
                                    }
                                    else
                                    {
                                        item.ConfigExpandedNodeId = new ExpandedNodeId(item.ConfigNodeId.Identifier, item.ConfigNodeId.NamespaceIndex, namespaceUri, 0);
                                        item.ConfigType = OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.ExpandedNodeId;
                                    }
                                }
                                item.State = OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Unmonitored;
                            }

                            // lookup namespace index if ExpandedNodeId format has been used and build NodeId identifier.
                            if (item.ConfigType == OpcUaMonitoredItemWrapper.OpcMonitoredItemConfigurationType.ExpandedNodeId)
                            {
                                int namespaceIndex = _namespaceTable.GetIndex(item.ConfigExpandedNodeId?.NamespaceUri);
                                if (namespaceIndex < 0)
                                {
                                    Program.Instance.Logger.Warning($"Syntax or namespace URI of ExpandedNodeId '{item.ConfigExpandedNodeId.ToString()}' is invalid and will be ignored.");
                                    continue;
                                }
                                currentNodeId = new NodeId(item.ConfigExpandedNodeId.Identifier, (ushort)namespaceIndex);
                            }
                            else
                            {
                                currentNodeId = item.ConfigNodeId;
                                var ns = _namespaceTable.GetString(currentNodeId.NamespaceIndex);
                                item.ConfigExpandedNodeId = new ExpandedNodeId(currentNodeId, ns);
                            }

                            // if configured, get the DisplayName for the node, otherwise use the nodeId
                            Node node;
                            if (string.IsNullOrEmpty(item.DisplayName))
                            {
                                if (SettingsConfiguration.FetchOpcNodeDisplayName == true)
                                {
                                    node = OpcUaClientSession.ReadNode(currentNodeId);
                                    item.DisplayName = node.DisplayName.Text ?? currentNodeId.ToString();
                                }
                                else
                                {
                                    item.DisplayName = currentNodeId.ToString();
                                }
                            }

                            // handle skip first request
                            item.SkipNextEvent = item.SkipFirst;

                            // create a heartbeat timer, but no start it
                            if (item.HeartbeatInterval > 0)
                            {
                                item.HeartbeatSendTimer = new Timer(item.HeartbeatSend, null, Timeout.Infinite, Timeout.Infinite);
                            }

                            // add the new monitored item.
                            MonitoredItem monitoredItem = new MonitoredItem() {
                                StartNodeId = currentNodeId,
                                AttributeId = item.AttributeId,
                                DisplayName = item.DisplayName,
                                MonitoringMode = item.MonitoringMode,
                                SamplingInterval = item.RequestedSamplingInterval,
                                QueueSize = item.QueueSize,
                                DiscardOldest = item.DiscardOldest
                            };
                            monitoredItem.Notification += item.Notification;

                            opcSubscription.OpcUaClientSubscription.AddItem(monitoredItem);
                            if (index % 10000 == 0 || index == (unmonitoredItems.Length - 1))
                            {
                                opcSubscription.OpcUaClientSubscription.SetPublishingMode(true);
                                opcSubscription.OpcUaClientSubscription.ApplyChanges();
                            }
                            item.OpcUaClientMonitoredItem = monitoredItem;
                            item.State = OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Monitored;
                            item.EndpointUrl = EndpointUrl;
                            Program.Instance.Logger.Verbose($"Created monitored item for node '{currentNodeId.ToString()}' in subscription with id '{opcSubscription.OpcUaClientSubscription.Id}' on endpoint '{EndpointUrl}' (version: {NodeConfigVersion:X8})");
                            if (item.RequestedSamplingInterval != monitoredItem.SamplingInterval)
                            {
                                Program.Instance.Logger.Information($"Sampling interval: requested: {item.RequestedSamplingInterval}; revised: {monitoredItem.SamplingInterval}");
                                item.SamplingInterval = monitoredItem.SamplingInterval;
                            }
                            if (index % 10000 == 0)
                            {
                                Program.Instance.Logger.Information($"Now monitoring {monitoredItemsCount + index} items in subscription with id '{opcSubscription.OpcUaClientSubscription.Id}'");
                            }
                        }
                        catch (ServiceResultException sre)
                        {
                            switch ((uint)sre.Result.StatusCode)
                            {
                                case StatusCodes.BadSessionIdInvalid:
                                    {
                                        Program.Instance.Logger.Information($"Session with Id {OpcUaClientSession.SessionId} is no longer available on endpoint '{EndpointUrl}'. Cleaning up.");
                                        // clean up the session
                                        InternalDisconnect();
                                        break;
                                    }
                                case StatusCodes.BadSubscriptionIdInvalid:
                                    {
                                        Program.Instance.Logger.Information($"Subscription with Id {opcSubscription.OpcUaClientSubscription.Id} is no longer available on endpoint '{EndpointUrl}'. Cleaning up.");
                                        // clean up the session/subscription
                                        InternalDisconnect();
                                        break;
                                    }
                                case StatusCodes.BadNodeIdInvalid:
                                case StatusCodes.BadNodeIdUnknown:
                                    {
                                        Program.Instance.Logger.Error($"Failed to monitor node '{currentNodeId}' on endpoint '{EndpointUrl}'.");
                                        Program.Instance.Logger.Error($"OPC UA ServiceResultException is '{sre.Result}'. Please check your publisher configuration for this node.");
                                        break;
                                    }
                                default:
                                    {
                                        Program.Instance.Logger.Error($"Unhandled OPC UA ServiceResultException '{sre.Result}' when monitoring node '{currentNodeId}' on endpoint '{EndpointUrl}'. Continue.");
                                        break;
                                    }
                            }
                        }
                        catch (Exception e)
                        {
                            Program.Instance.Logger.Error(e, $"Failed to monitor node '{currentNodeId}' on endpoint '{EndpointUrl}'");
                        }
                    }

                    stopWatch.Stop();
                    if (haveUnmonitoredItems == true)
                    {
                        monitoredItemsCount = opcSubscription.OpcMonitoredItems.Count(i => (i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Monitored));
                        Program.Instance.Logger.Information($"Done processing unmonitored items on endpoint '{EndpointUrl}' took {stopWatch.ElapsedMilliseconds} msec. Now monitoring {monitoredItemsCount} items in subscription with id '{opcSubscription.OpcUaClientSubscription.Id}'.");
                    }
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
        }

        /// <summary>
        /// Checks if there are monitored nodes tagged to stop monitoring.
        /// </summary>
        public async Task StopMonitoringNodesAsync(CancellationToken ct)
        {
            bool sessionLocked = false;
            try
            {
                try
                {
                    sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                    // if shutdown is in progress, return
                    if (!sessionLocked || ct.IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    throw;
                }

                foreach (var opcSubscription in OpcSubscriptionWrappers)
                {
                    // remove items tagged to stop in the stack
                    var itemsToRemove = opcSubscription.OpcMonitoredItems.Where(i => i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.RemovalRequested).ToArray();
                    if (itemsToRemove.Any())
                    {
                        try
                        {
                            Program.Instance.Logger.Information($"Remove nodes in subscription with id {opcSubscription.OpcUaClientSubscription.Id} on endpoint '{EndpointUrl}'");
                            opcSubscription.OpcUaClientSubscription.RemoveItems(itemsToRemove.Select(i => i.OpcUaClientMonitoredItem));
                            Program.Instance.Logger.Information($"There are now {opcSubscription.OpcUaClientSubscription.MonitoredItemCount} monitored items in this subscription.");
                        }
                        catch (Exception ex)
                        {
                            // nodes may be tagged for stop before they are monitored, just continue
                            Program.Instance.Logger.Debug(ex, "Removing opc ua nodes from subscription caused exception");
                        }
                        // stop heartbeat timer for all items to remove
                        foreach (var itemToRemove in itemsToRemove)
                        {
                            itemToRemove.HeartbeatSendTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                        }
                        // remove them in our data structure
                        opcSubscription.OpcMonitoredItems.RemoveAll(i => i.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.RemovalRequested);
                        Interlocked.Increment(ref NodeConfigVersion);
                        Program.Instance.Logger.Information($"There are now {opcSubscription.OpcMonitoredItems.Count} items managed by publisher for this subscription. (version: {NodeConfigVersion:X8})");
                    }
                }
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
        }

        /// <summary>
        /// Checks if there are subscriptions without any monitored items and remove them.
        /// </summary>
        public async Task RemoveUnusedSubscriptionsAsync(CancellationToken ct)
        {
            bool sessionLocked = false;
            try
            {
                sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                // if shutdown is in progress, return
                if (!sessionLocked || ct.IsCancellationRequested)
                {
                    return;
                }

                // remove the subscriptions in the stack
                var subscriptionsToRemove = OpcSubscriptionWrappers.Where(i => i.OpcMonitoredItems.Count == 0).ToArray();
                if (subscriptionsToRemove.Any())
                {
                    try
                    {
                        Program.Instance.Logger.Information($"Remove unused subscriptions on endpoint '{EndpointUrl}'.");
                        OpcUaClientSession.RemoveSubscriptions(subscriptionsToRemove.Select(s => s.OpcUaClientSubscription));
                        Program.Instance.Logger.Information($"There are now {OpcUaClientSession.SubscriptionCount} subscriptions in this session.");
                    }
                    catch (Exception ex)
                    {
                        // subscriptions may be no longer required before they are created, just continue
                        Program.Instance.Logger.Debug(ex, "Removing subscription caused exception");
                    }
                }
                // remove them in our data structures
                OpcSubscriptionWrappers.RemoveAll(s => s.OpcMonitoredItems.Count == 0);
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }

        }

        /// <summary>
        /// Checks if there are session without any subscriptions and remove them.
        /// </summary>
        public async Task RemoveUnusedSessionsAsync(CancellationToken ct)
        {
            try
            {
                try
                {
                    await Program.Instance._nodeConfig.OpcSessionsListSemaphore.WaitAsync().ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                // if shutdown is in progress, return
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                // remove sessions in the stack
                var sessionsToRemove = Program.Instance._nodeConfig.OpcSessions.Where(s => s.OpcSubscriptionWrappers.Count == 0);
                foreach (var sessionToRemove in sessionsToRemove)
                {
                    Program.Instance.Logger.Information($"Remove unused session on endpoint '{EndpointUrl}'.");
                    await sessionToRemove.ShutdownAsync().ConfigureAwait(false);
                }
                // remove then in our data structures
                Program.Instance._nodeConfig.OpcSessions.RemoveAll(s => s.OpcSubscriptionWrappers.Count == 0);
            }
            finally
            {
                Program.Instance._nodeConfig?.OpcSessionsListSemaphore?.Release();
            }
        }

        /// <summary>
        /// Disconnects a session and removes all subscriptions on it and marks all nodes on those subscriptions
        /// as unmonitored.
        /// </summary>
        public async Task DisconnectAsync()
        {
            bool sessionLocked = await LockSessionAsync().ConfigureAwait(false);
            if (sessionLocked)
            {
                try
                {
                    InternalDisconnect();
                }
                catch (Exception e)
                {
                    Program.Instance.Logger.Error(e, $"Exception while disconnecting '{EndpointUrl}'.");
                }
                ReleaseSession();
            }
        }

        /// <summary>
        /// Returns the namespace index for a namespace URI.
        /// </summary>
        public int GetNamespaceIndexUnlocked(string namespaceUri)
        {
            return _namespaceTable.GetIndex(namespaceUri);
        }

        /// <summary>
        /// Internal disconnect method. Caller must have taken the _opcSessionSemaphore.
        /// </summary>
        private void InternalDisconnect()
        {
            try
            {
                foreach (var opcSubscription in OpcSubscriptionWrappers)
                {
                    try
                    {
                        if (opcSubscription.OpcUaClientSubscription != null)
                        {
                            OpcUaClientSession.RemoveSubscription(opcSubscription.OpcUaClientSubscription);
                        }
                    }
#pragma warning disable RECS0022 // A catch clause that catches System.Exception and has an empty body
                    catch
#pragma warning restore RECS0022 // A catch clause that catches System.Exception and has an empty body
                    {
                        // the session might be already invalidated. ignore.
                    }
                    opcSubscription.OpcUaClientSubscription = null;

                    // mark all monitored items as unmonitored
                    foreach (var opcMonitoredItem in opcSubscription.OpcMonitoredItems)
                    {
                        // tag all monitored items as unmonitored
                        if (opcMonitoredItem.State == OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Monitored)
                        {
                            opcMonitoredItem.State = OpcUaMonitoredItemWrapper.OpcMonitoredItemState.Unmonitored;
                        }
                    }
                }
                try
                {
                    OpcUaClientSession?.Close();
                }
                catch (Exception ex)
                {
                    // the session might be already invalidated. ignore.
                    Program.Instance.Logger.Debug(ex, "Closing OPC UA client session {SessionId} caused exception", OpcUaClientSession?.SessionId);
                }
                OpcUaClientSession = null;
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            State = SessionState.Disconnected;
            MissedKeepAlives = 0;
        }

        /// <summary>
        /// Adds a node to be monitored. If there is no subscription with the requested publishing interval,
        /// one is created.
        /// </summary>
        public async Task<HttpStatusCode> AddNodeForMonitoringAsync(NodeId nodeId, ExpandedNodeId expandedNodeId,
            int? opcPublishingInterval, int? opcSamplingInterval, string displayName,
            int? heartbeatInterval, bool? skipFirst, CancellationToken ct)
        {
            string logPrefix = "AddNodeForMonitoringAsync:";
            bool sessionLocked = false;
            try
            {
                sessionLocked = await LockSessionAsync().ConfigureAwait(false);
                if (!sessionLocked || ct.IsCancellationRequested)
                {
                    return HttpStatusCode.Gone;
                }

                // check if there is already a subscription with the same publishing interval, which can be used to monitor the node
                int opcPublishingIntervalForNode = opcPublishingInterval ?? SettingsConfiguration.DefaultOpcPublishingInterval;
                OpcUaSubscriptionWrapper opcSubscription = OpcSubscriptionWrappers.FirstOrDefault(s => s.PublishingInterval == opcPublishingIntervalForNode);

                // if there was none found, create one
                if (opcSubscription == null)
                {
                    if (opcPublishingInterval == null)
                    {
                        Program.Instance.Logger.Information($"{logPrefix} No matching subscription with default publishing interval found.");
                        Program.Instance.Logger.Information($"Create a new subscription with a default publishing interval.");
                    }
                    else
                    {
                        Program.Instance.Logger.Information($"{logPrefix} No matching subscription with publishing interval of {opcPublishingInterval} found.");
                        Program.Instance.Logger.Information($"Create a new subscription with a publishing interval of {opcPublishingInterval}.");
                    }
                    opcSubscription = new OpcUaSubscriptionWrapper(opcPublishingInterval);
                    OpcSubscriptionWrappers.Add(opcSubscription);
                }

                // create objects for publish check
                ExpandedNodeId expandedNodeIdCheck = expandedNodeId;
                NodeId nodeIdCheck = nodeId;
                if (State == SessionState.Connected)
                {
                    if (expandedNodeId == null)
                    {
                        string namespaceUri = _namespaceTable.ToArray().ElementAtOrDefault(nodeId.NamespaceIndex);
                        expandedNodeIdCheck = new ExpandedNodeId(nodeId.Identifier, nodeId.NamespaceIndex, namespaceUri, 0);
                    }
                    if (nodeId == null)
                    {
                        nodeIdCheck = new NodeId(expandedNodeId.Identifier, (ushort)(_namespaceTable.GetIndex(expandedNodeId.NamespaceUri)));
                    }
                }

                // if it is already published, we do nothing, else we create a new monitored item
                // todo check properties and update
                if (!IsNodePublishedInSessionInternal(nodeIdCheck, expandedNodeIdCheck))
                {
                    OpcUaMonitoredItemWrapper opcMonitoredItem = null;
                    // add a new item to monitor
                    if (expandedNodeId == null)
                    {
                        opcMonitoredItem = new OpcUaMonitoredItemWrapper(nodeId, EndpointUrl, opcSamplingInterval, displayName, heartbeatInterval, skipFirst);
                    }
                    else
                    {
                        opcMonitoredItem = new OpcUaMonitoredItemWrapper(expandedNodeId, EndpointUrl, opcSamplingInterval, displayName, heartbeatInterval, skipFirst);
                    }
                    opcSubscription.OpcMonitoredItems.Add(opcMonitoredItem);
                    Interlocked.Increment(ref NodeConfigVersion);
                    Program.Instance.Logger.Debug($"{logPrefix} Added item with nodeId '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' for monitoring.");

                    // trigger the actual OPC communication with the server to be done
                    ConnectAndMonitorSession.Set();
                    return HttpStatusCode.Accepted;
                }
                else
                {
                    Program.Instance.Logger.Debug($"{logPrefix} Node with Id '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' is already monitored.");
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception while trying to add node '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' for monitoring.");
                return HttpStatusCode.InternalServerError;
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Tags a monitored node to stop monitoring and remove it.
        /// </summary>
        public async Task<HttpStatusCode> RequestMonitorItemRemovalAsync(NodeId nodeId, ExpandedNodeId expandedNodeId, CancellationToken ct, bool takeLock = true)
        {
            HttpStatusCode result = HttpStatusCode.Gone;
            bool sessionLocked = false;
            try
            {
                if (takeLock)
                {
                    sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                    if (!sessionLocked || ct.IsCancellationRequested)
                    {
                        return HttpStatusCode.Gone;
                    }
                }

                // create objects for publish check
                ExpandedNodeId expandedNodeIdCheck = expandedNodeId;
                NodeId nodeIdCheck = nodeId;
                if (State == SessionState.Connected)
                {
                    if (expandedNodeId == null)
                    {
                        string namespaceUri = _namespaceTable.ToArray().ElementAtOrDefault(nodeId.NamespaceIndex);
                        expandedNodeIdCheck = new ExpandedNodeId(nodeIdCheck, namespaceUri, 0);
                    }
                    if (nodeId == null)
                    {
                        nodeIdCheck = new NodeId(expandedNodeId.Identifier, (ushort)(_namespaceTable.GetIndex(expandedNodeId.NamespaceUri)));
                    }

                }

                // if node is not published return success
                if (!IsNodePublishedInSessionInternal(nodeIdCheck, expandedNodeIdCheck))
                {
                    Program.Instance.Logger.Information($"RequestMonitorItemRemoval: Node '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' is not monitored.");
                    return HttpStatusCode.OK;
                }

                // tag all monitored items with nodeId to stop monitoring.
                // if the node to tag is specified as NodeId, it will also tag nodes configured in ExpandedNodeId format.
                foreach (var opcSubscription in OpcSubscriptionWrappers)
                {
                    var opcMonitoredItems = opcSubscription.OpcMonitoredItems.Where(m => { return m.IsMonitoringThisNode(nodeIdCheck, expandedNodeIdCheck, _namespaceTable); });
                    foreach (var opcMonitoredItem in opcMonitoredItems)
                    {
                        // tag it for removal.
                        opcMonitoredItem.State = OpcUaMonitoredItemWrapper.OpcMonitoredItemState.RemovalRequested;
                        Program.Instance.Logger.Information($"RequestMonitorItemRemoval: Node with id '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' tagged to stop monitoring.");
                        result = HttpStatusCode.Accepted;
                    }
                }

                // trigger the actual OPC communication with the server to be done
                ConnectAndMonitorSession.Set();
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, $"RequestMonitorItemRemoval: Exception while trying to tag node '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' to stop monitoring.");
                result = HttpStatusCode.InternalServerError;
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
            return result;
        }

        /// <summary>
        /// Checks if the node specified by either the given NodeId or ExpandedNodeId on the given endpoint is published in the session. Caller to take session semaphore.
        /// </summary>
        private bool IsNodePublishedInSessionInternal(NodeId nodeId, ExpandedNodeId expandedNodeId)
        {
            try
            {
                foreach (var opcSubscription in OpcSubscriptionWrappers)
                {
                    if (opcSubscription.OpcMonitoredItems.Any(m => { return m.IsMonitoringThisNode(nodeId, expandedNodeId, _namespaceTable); }))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            return false;
        }

        /// <summary>
        /// Checks if the node specified by either the given NodeId or ExpandedNodeId on the given endpoint is published in the session.
        /// </summary>
        public bool IsNodePublishedInSession(NodeId nodeId, ExpandedNodeId expandedNodeId)
        {
            bool result = false;
            bool sessionLocked = false;
            try
            {
                sessionLocked = LockSessionAsync().Result;

                if (sessionLocked && !_sessionCancelationToken.IsCancellationRequested)
                {
                    result = IsNodePublishedInSessionInternal(nodeId, expandedNodeId);
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            finally
            {
                if (sessionLocked)
                {
                    ReleaseSession();
                }
            }
            return result;
        }

        /// <summary>
        /// Checks if the node specified by either the given NodeId or ExpandedNodeId on the given endpoint is published.
        /// </summary>
        public static bool IsNodePublished(NodeId nodeId, ExpandedNodeId expandedNodeId, string endpointUrl)
        {
            try
            {
                Program.Instance._nodeConfig.OpcSessionsListSemaphore.Wait();

                // itereate through all sessions, subscriptions and monitored items and create config file entries
                foreach (var opcSession in Program.Instance._nodeConfig.OpcSessions)
                {
                    if (opcSession.EndpointUrl.Equals(endpointUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        if (opcSession.IsNodePublishedInSession(nodeId, expandedNodeId))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Exception");
            }
            finally
            {
                Program.Instance._nodeConfig.OpcSessionsListSemaphore.Release();
            }
            return false;
        }

        /// <summary>
        /// Shutdown the current session if it is connected.
        /// </summary>
        public async Task ShutdownAsync()
        {
            bool sessionLocked = false;
            try
            {
                sessionLocked = await LockSessionAsync().ConfigureAwait(false);

                // if the session is connected, close it.
                if (sessionLocked && (State == SessionState.Connecting || State == SessionState.Connected))
                {
                    try
                    {
                        foreach (var opcSubscription in OpcSubscriptionWrappers)
                        {
                            Program.Instance.Logger.Information($"Removing {opcSubscription.OpcUaClientSubscription.MonitoredItemCount} monitored items from subscription with id '{opcSubscription.OpcUaClientSubscription.Id}'.");
                            opcSubscription.OpcUaClientSubscription.RemoveItems(opcSubscription.OpcUaClientSubscription.MonitoredItems);
                        }
                        Program.Instance.Logger.Information($"Removing {OpcUaClientSession.SubscriptionCount} subscriptions from session.");
                        while (OpcSubscriptionWrappers.Count > 0)
                        {
                            OpcUaSubscriptionWrapper opcSubscription = OpcSubscriptionWrappers.ElementAt(0);
                            OpcSubscriptionWrappers.RemoveAt(0);
                            Subscription opcUaClientSubscription = opcSubscription.OpcUaClientSubscription;
                            opcUaClientSubscription.Delete(true);
                        }
                        Program.Instance.Logger.Information($"Closing session to endpoint URI '{EndpointUrl}' closed successfully.");
                        OpcUaClientSession.Close();
                        State = SessionState.Disconnected;
                        Program.Instance.Logger.Information($"Session to endpoint URI '{EndpointUrl}' closed successfully.");
                    }
                    catch (Exception e)
                    {
                        Program.Instance.Logger.Error(e, $"Exception while closing session to endpoint '{EndpointUrl}'.");
                        State = SessionState.Disconnected;
                        return;
                    }
                }
            }
            finally
            {
                if (sessionLocked)
                {
                    // cancel all threads waiting on the session semaphore
                    _sessionCancelationTokenSource.Cancel();
                    _opcSessionSemaphore.Release();
                }
            }
        }

        /// <summary>
        /// Create a subscription in the session.
        /// </summary>
        private Subscription CreateSubscription(int requestedPublishingInterval, out int revisedPublishingInterval)
        {
            Subscription subscription = new Subscription(OpcUaClientSession.DefaultSubscription) {
                PublishingInterval = requestedPublishingInterval,
            };
            // need to happen before the create to set the Session property.
            OpcUaClientSession.AddSubscription(subscription);
            subscription.Create();
            Program.Instance.Logger.Information($"Created subscription with id {subscription.Id} on endpoint '{EndpointUrl}'");
            if (requestedPublishingInterval != subscription.PublishingInterval)
            {
                Program.Instance.Logger.Information($"Publishing interval: requested: {requestedPublishingInterval}; revised: {subscription.PublishingInterval}");
            }
            revisedPublishingInterval = subscription.PublishingInterval;
            return subscription;
        }

        /// <summary>
        /// Handler for the standard "keep alive" event sent by all OPC UA servers
        /// </summary>
        private void StandardClient_KeepAlive(Session session, KeepAliveEventArgs eventArgs)
        {
            // Ignore if we are shutting down.
            if (Program.Instance.ShutdownTokenSource.IsCancellationRequested == true)
            {
                return;
            }

            if (eventArgs != null && session != null && session.ConfiguredEndpoint != null && OpcUaClientSession != null)
            {
                try
                {
                    if (!ServiceResult.IsGood(eventArgs.Status))
                    {
                        Program.Instance.Logger.Warning($"Session endpoint: {session.ConfiguredEndpoint.EndpointUrl} has Status: {eventArgs.Status}");
                        Program.Instance.Logger.Information($"Outstanding requests: {session.OutstandingRequestCount}, Defunct requests: {session.DefunctRequestCount}");
                        Program.Instance.Logger.Information($"Good publish requests: {session.GoodPublishRequestCount}, KeepAlive interval: {session.KeepAliveInterval}");
                        Program.Instance.Logger.Information($"SessionId: {session.SessionId}");
                        Program.Instance.Logger.Information($"Session State: {State}");

                        if (State == SessionState.Connected)
                        {
                            MissedKeepAlives++;
                            Program.Instance.Logger.Information($"Missed KeepAlives: {MissedKeepAlives}");
                        }
                    }
                    else
                    {
                        if (MissedKeepAlives != 0)
                        {
                            // Reset missed keep alive count
                            Program.Instance.Logger.Information($"Session endpoint: {session.ConfiguredEndpoint.EndpointUrl} got a keep alive after {MissedKeepAlives} {(MissedKeepAlives == 1 ? "was" : "were")} missed.");
                            MissedKeepAlives = 0;
                        }
                    }
                }
                catch (Exception e)
                {
                    Program.Instance.Logger.Error(e, $"Exception in keep alive handling for endpoint '{session.ConfiguredEndpoint.EndpointUrl}'. ('{e.Message}'");
                }
            }
            else
            {
                Program.Instance.Logger.Warning("Keep alive arguments seems to be wrong.");
            }
        }

        /// <summary>
        /// Take the session semaphore.
        /// </summary>
        public async Task<bool> LockSessionAsync()
        {
            try
            {
                await _opcSessionSemaphore.WaitAsync(_sessionCancelationToken).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
            if (_sessionCancelationToken.IsCancellationRequested)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Release the session semaphore.
        /// </summary>
        public void ReleaseSession()
        {
            _opcSessionSemaphore?.Release();
        }

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
                            nodeStatusCode = await AddNodeForMonitoringAsync(nodeId, null,
                                node.OpcPublishingInterval, node.OpcSamplingInterval, node.DisplayName,
                                node.HeartbeatInterval, node.SkipFirst,
                                Program.Instance.ShutdownTokenSource.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            // add the node info to the subscription with the default publishing interval, execute syncronously
                            Program.Instance.Logger.Debug($"{logPrefix} Request to monitor item with ExpandedNodeId '{node.Id}' (PublishingInterval: {node.OpcPublishingInterval.ToString() ?? "--"}, SamplingInterval: {node.OpcSamplingInterval.ToString() ?? "--"})");
                            nodeStatusCode = await AddNodeForMonitoringAsync(null, expandedNodeId,
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

        /// <summary>
        /// List of monitored items on this subscription.
        /// </summary>
        public List<OpcUaMonitoredItemWrapper> OpcMonitoredItems = new List<OpcUaMonitoredItemWrapper>();

        /// <summary>
        /// The OPC UA stack subscription object.
        /// </summary>
        public Subscription OpcUaClientSubscription;

        private UAClient()
        {
            // do nothing
        }

        private static UAClient _instance = new UAClient();

        private SemaphoreSlim _opcSessionSemaphore;
        private CancellationTokenSource _sessionCancelationTokenSource;
        private readonly CancellationToken _sessionCancelationToken;
        private readonly NamespaceTable _namespaceTable;
        private readonly Task _connectAndMonitorAsync;
    }
}
