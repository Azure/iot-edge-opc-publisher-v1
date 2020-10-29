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

    public class UAClient
    {
        public UAClient(ApplicationConfiguration applicationConfig)
        {
            _uaApplicationConfiguration = applicationConfig;
        }

        private Session FindSession(string endpointUrl)
        {
            EndpointDescription selectedEndpoint;
            try
            {
                if (_endpointDescriptionCache.ContainsKey(endpointUrl))
                {
                    selectedEndpoint = _endpointDescriptionCache[endpointUrl];
                }
                else
                {
                    // use a discovery client to connect to the server and discover all its endpoints, then pick the one with the higest security
                    selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, SettingsConfiguration.UseSecurity);

                    // add to cache
                    _endpointDescriptionCache[endpointUrl] = selectedEndpoint;
                }
            }
            catch (Exception ex)
            {
                Program.Instance.Logger.Error(ex, $"Cannot reach server on endpoint {endpointUrl}. Please make sure your OPC UA server is running and accessible.");
                return null;
            }

            if (selectedEndpoint == null)
            {
                // could not get the requested endpoint
                return null;
            }

            // check if we already have a session for the requested endpoint
            lock (_sessionsLock)
            {
                foreach (Session session in _sessions)
                {
                    ConfiguredEndpoint configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create());
                    if (session.ConfiguredEndpoint.EndpointUrl == configuredEndpoint.EndpointUrl)
                    {
                        // return the existing session
                        return session;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Connects the session if it is disconnected.
        /// </summary>
        private async Task<Session> ConnectSessionAsync(string endpointUrl, NetworkCredential credentials)
        {
            // check if we have the required session already
            Session existingSession = FindSession(endpointUrl);
            if (existingSession != null)
            {
                return existingSession;
            }

            EndpointDescription selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, SettingsConfiguration.UseSecurity);
            ConfiguredEndpoint configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create());
            Program.Instance.Logger.Information($"Connecting session on endpoint '{configuredEndpoint.EndpointUrl}'.");

            uint timeout = (uint)_uaApplicationConfiguration.ClientConfiguration.DefaultSessionTimeout;
            Program.Instance.Logger.Information($"Create {(SettingsConfiguration.UseSecurity ? "secured" : "unsecured")} session for endpoint URI '{configuredEndpoint.EndpointUrl}' with timeout of {timeout} ms.");

            UserIdentity userIdentity = null;
            if (credentials == null)
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }
            else
            {
                userIdentity = new UserIdentity(credentials.UserName, credentials.Password);
            }

            Session newSession = null;
            try
            {
                newSession = await Session.Create(
                    _uaApplicationConfiguration,
                    configuredEndpoint,
                    true,
                    false,
                    _uaApplicationConfiguration.ApplicationName,
                    timeout,
                    userIdentity,
                    null);
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, $"Session creation to endpoint '{configuredEndpoint.EndpointUrl}' failed. Please verify if server is up and Publisher configuration is correct.");
                return null;
            }

            Program.Instance.Logger.Information($"Session successfully created with Id {newSession.SessionId}.");
            if (!selectedEndpoint.EndpointUrl.Equals(configuredEndpoint.EndpointUrl.OriginalString, StringComparison.OrdinalIgnoreCase))
            {
                Program.Instance.Logger.Information($"the Server has updated the EndpointUrl to '{selectedEndpoint.EndpointUrl}'");
            }

            // init object state and install keep alive
            newSession.KeepAlive += StandardClient_KeepAlive;

            // add the session to our list
            lock (_sessionsLock)
            {
                _sessions.Add(newSession);
                Metrics.NumberOfOpcSessionsConnected = _sessions.Count;
            }

            return newSession;
        }

        public void UnpublishAllNodes()
        {
            // loop through all sessions
            lock (_sessionsLock)
            {
                _heartbeats.Clear();

                while (_sessions.Count > 0)
                {
                    Session session = _sessions[0];
                    while (session.SubscriptionCount > 0)
                    {
                        Subscription subscription = session.Subscriptions.First();
                        while (subscription.MonitoredItemCount > 0)
                        {
                            subscription.RemoveItem(subscription.MonitoredItems.First());
                            subscription.ApplyChanges();
                        }
                        Metrics.NumberOfOpcMonitoredItemsMonitored -= (int)subscription.MonitoredItemCount;

                        session.RemoveSubscription(subscription);
                        Metrics.NumberOfOpcSubscriptionsConnected--;
                    }

                    string endpoint = session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
                    session.Close();
                    _sessions.Remove(session);
                    Metrics.NumberOfOpcSessionsConnected--;

                    Program.Instance.Logger.Information($"Session to endpoint URI '{endpoint}' closed successfully.");
                }
            }
        }

        /// <summary>
        /// Create a subscription in the session.
        /// </summary>
        private Subscription CreateSubscription(Session session, ref int publishingInterval)
        {
            Subscription subscription = new Subscription(session.DefaultSubscription) {
                PublishingInterval = publishingInterval,
            };

            // add needs to happen before create to set the Session property
            session.AddSubscription(subscription);
            subscription.Create();

            Metrics.NumberOfOpcSubscriptionsConnected++;

            Program.Instance.Logger.Information($"Created subscription with id {subscription.Id} on endpoint '{session.Endpoint.EndpointUrl}'");
            
            if (publishingInterval != subscription.PublishingInterval)
            {
                Program.Instance.Logger.Information($"Publishing interval: requested: {publishingInterval}; revised: {subscription.PublishingInterval}");
            }
            
            return subscription;
        }

        /// <summary>
        /// Handler for the standard "keep alive" event sent by all OPC UA servers
        /// </summary>
        private void StandardClient_KeepAlive(Session session, KeepAliveEventArgs eventArgs)
        {
            if (eventArgs != null && session != null && session.ConfiguredEndpoint != null)
            {
                try
                {
                    string endpoint = session.ConfiguredEndpoint.EndpointUrl.AbsoluteUri;
                    if (!ServiceResult.IsGood(eventArgs.Status))
                    {
                        Program.Instance.Logger.Warning($"Session endpoint: {session.ConfiguredEndpoint.EndpointUrl} has Status: {eventArgs.Status}");
                        Program.Instance.Logger.Information($"Outstanding requests: {session.OutstandingRequestCount}, Defunct requests: {session.DefunctRequestCount}");
                        Program.Instance.Logger.Information($"Good publish requests: {session.GoodPublishRequestCount}, KeepAlive interval: {session.KeepAliveInterval}");
                        Program.Instance.Logger.Information($"SessionId: {session.SessionId}");
                        Program.Instance.Logger.Information($"Session State: {session.Connected}");

                        if (session.Connected)
                        {
                            if (!_missedKeepAlives.ContainsKey(endpoint))
                            {
                                _missedKeepAlives[endpoint] = 0;
                            }
                            _missedKeepAlives[endpoint]++;
                            Program.Instance.Logger.Information($"Missed KeepAlives: {_missedKeepAlives[endpoint]}");
                        }
                    }
                    else
                    {
                        if (_missedKeepAlives.ContainsKey(endpoint) && _missedKeepAlives[endpoint] != 0)
                        {
                            // Reset missed keep alive count
                            Program.Instance.Logger.Information($"Session endpoint: {endpoint} got a keep alive after {_missedKeepAlives[endpoint]} {(_missedKeepAlives[endpoint] == 1 ? "was" : "were")} missed.");
                            _missedKeepAlives[session.ConfiguredEndpoint.EndpointUrl.ToString()] = 0;
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

        public HttpStatusCode PublishNode(NodePublishingConfigurationModel node)
        {
            // find or create the session we need to monitor the node
            Session opcSession = ConnectSessionAsync(node.EndpointUrl, node.AuthCredential).Result;
            if (opcSession == null)
            {
                // couldn't create the session
                return HttpStatusCode.Gone;
            }
            
            // support legacy format
            if ((node.NodeId == null) && (node.ExpandedNodeId != null))
            {
                node.NodeId = new NodeId(node.ExpandedNodeId.Identifier, node.ExpandedNodeId.NamespaceIndex);
            }

            NodeId nodeId = null;
            ExpandedNodeId expandedNodeId = null;
            bool isNodeIdFormat = true;
            try
            {
                if (node.NodeId.Identifier.ToString().Contains("nsu=", StringComparison.InvariantCulture))
                {
                    expandedNodeId = ExpandedNodeId.Parse(node.NodeId.Identifier.ToString());
                    isNodeIdFormat = false;
                }
                else
                {
                    nodeId = node.NodeId;
                    isNodeIdFormat = true;
                }
            }
            catch (Exception e)
            {
                string statusMessage = $"Exception ({e.Message}) while formatting node '{node.NodeId}'!";
                Program.Instance.Logger.Error(e, $"PublishNode: {statusMessage}");
            }

            if (isNodeIdFormat)
            {
                // add the node info to the subscription with the default publishing interval, execute syncronously
                Program.Instance.Logger.Debug($"PublishNode: Request to monitor item with NodeId '{node.NodeId}' (PublishingInterval: {node.OpcPublishingInterval.ToString() ?? "--"}, SamplingInterval: {node.OpcSamplingInterval.ToString() ?? "--"})");
                return AddNodeForMonitoring(
                    opcSession,
                    nodeId,
                    null,
                    node.OpcPublishingInterval,
                    node.OpcSamplingInterval,
                    node.DisplayName,
                    node.HeartbeatInterval,
                    node.SkipFirst);
            }
            else
            {
                // add the node info to the subscription with the default publishing interval, execute syncronously
                Program.Instance.Logger.Debug($"PublishNode: Request to monitor item with ExpandedNodeId '{node.NodeId}' (PublishingInterval: {node.OpcPublishingInterval.ToString() ?? "--"}, SamplingInterval: {node.OpcSamplingInterval.ToString() ?? "--"})");
                return AddNodeForMonitoring(
                    opcSession,
                    null,
                    expandedNodeId,
                    node.OpcPublishingInterval,
                    node.OpcSamplingInterval,
                    node.DisplayName,
                    node.HeartbeatInterval,
                    node.SkipFirst);
            }
        }

        /// <summary>
        /// Adds a node to be monitored. If there is no subscription with the requested publishing interval,
        /// one is created.
        /// </summary>
        private HttpStatusCode AddNodeForMonitoring(
            Session session,
            NodeId nodeId,
            ExpandedNodeId expandedNodeId,
            int opcPublishingInterval,
            int opcSamplingInterval,
            string displayName,
            int heartbeatInterval,
            bool skipFirst)
        {
            string logPrefix = "AddNodeForMonitoringAsync:";
            Subscription opcSubscription = null;

            try
            {
                // check if there is already a subscription with the same publishing interval, which can be used to monitor the node
                int opcPublishingIntervalForNode = (opcPublishingInterval == 0)? SettingsConfiguration.DefaultOpcPublishingInterval : opcPublishingInterval;
                foreach (Subscription subscription in session.Subscriptions)
                {
                    if (subscription.PublishingInterval == opcPublishingIntervalForNode)
                    {
                        opcSubscription = subscription;
                        break;
                    }
                }

                // if there was none found, create one
                if (opcSubscription == null)
                {
                    Program.Instance.Logger.Information($"{logPrefix} No matching subscription with publishing interval of {opcPublishingInterval} found, creating a new one.");
                    opcSubscription = CreateSubscription(session, ref opcPublishingIntervalForNode);
                }

                // create objects for publish check
                if (session.Connected)
                {
                    // update cached namespace table
                    session.FetchNamespaceTables();

                    if (expandedNodeId == null)
                    {
                        string namespaceUri = session.NamespaceUris.ToArray().ElementAtOrDefault(nodeId.NamespaceIndex);
                        expandedNodeId = new ExpandedNodeId(nodeId.Identifier, nodeId.NamespaceIndex, namespaceUri, 0);
                    }

                    if (nodeId == null)
                    {
                        nodeId = new NodeId(expandedNodeId.Identifier, (ushort)(session.NamespaceUris.GetIndex(expandedNodeId.NamespaceUri)));
                    }
                }

                // resolve all node and namespace references in the select and where clauses
                EventFilter eventFilter = new EventFilter();
                foreach (var selectClause in unmonitoredEvent.EventConfiguration.SelectClauses)
                {
                    SimpleAttributeOperand simpleAttributeOperand = new SimpleAttributeOperand();
                    simpleAttributeOperand.AttributeId = selectClause.AttributeId.ResolveAttributeId();
                    simpleAttributeOperand.IndexRange = selectClause.IndexRange;
                    NodeId typeId = selectClause.TypeId.ToNodeId(_namespaceTable);
                    simpleAttributeOperand.TypeDefinitionId = new NodeId(typeId);
                    QualifiedNameCollection browsePaths = new QualifiedNameCollection();
                    foreach (var browsePath in selectClause.BrowsePaths)
                    {
                        browsePaths.Add(QualifiedName.Parse(browsePath));
                    }
                    simpleAttributeOperand.BrowsePath = browsePaths;
                    eventFilter.SelectClauses.Add(simpleAttributeOperand);
                }
                foreach (var whereClauseElement in unmonitoredEvent.EventConfiguration.WhereClause)
                {
                    ContentFilterElement contentFilterElement = new ContentFilterElement();
                    contentFilterElement.FilterOperator = whereClauseElement.Operator.ResolveFilterOperator();
                    switch (contentFilterElement.FilterOperator)
                    {
                        case FilterOperator.OfType:
                        case FilterOperator.InView:
                            if (whereClauseElement.Operands.Count != 1)
                            {
                                Program.Instance.Logger.Error($"The where clause element '{whereClauseElement.ToString()}' must contain 1 operands.");
                                continue;
                            }
                            FilterOperand[] filterOperands = new FilterOperand[1];
                            TypeInfo typeInfo = new TypeInfo(BuiltInType.NodeId, ValueRanks.Scalar);
                            filterOperands[0] = whereClauseElement.Operands[0].GetOperand(typeInfo);
                            //filterOperands[0] = whereClauseElement.Operands[0].GetOperand(DataTypeIds.NodeId);
                            eventFilter.WhereClause.Push(contentFilterElement.FilterOperator, filterOperands);
                            break;
                        case FilterOperator.Equals:
                        case FilterOperator.IsNull:
                        case FilterOperator.GreaterThan:
                        case FilterOperator.LessThan:
                        case FilterOperator.GreaterThanOrEqual:
                        case FilterOperator.LessThanOrEqual:
                        case FilterOperator.Like:
                        case FilterOperator.Not:
                        case FilterOperator.Between:
                        case FilterOperator.InList:
                        case FilterOperator.And:
                        case FilterOperator.Or:
                        case FilterOperator.Cast:
                        case FilterOperator.BitwiseAnd:
                        case FilterOperator.BitwiseOr:
                        //case FilterOperator.InView:
                        //case FilterOperator.OfType:
                        case FilterOperator.RelatedTo:
                        default:
                            Program.Instance.Logger.Error($"The operator '{contentFilterElement.FilterOperator.ToString()}' is not supported.");
                            break;
                    }
                }

                // if it is already published, we do nothing, else we create a new monitored item
                foreach (MonitoredItem monitoredItem in opcSubscription.MonitoredItems)
                {
                    if (monitoredItem.ResolvedNodeId == nodeId)
                    {
                        Program.Instance.Logger.Debug($"{logPrefix} Node with Id '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' is already monitored.");
                        return HttpStatusCode.OK;
                    }
                }

                MonitoredItem newMonitoredItem = new MonitoredItem(opcSubscription.DefaultItem) {
                    StartNodeId = nodeId,
                    AttributeId = Attributes.Value,
                    DisplayName = displayName,
                    SamplingInterval = (int)opcSamplingInterval
                };
                newMonitoredItem.Notification += MonitoredItemNotification.DataChangedEventHandler;

                opcSubscription.AddItem(newMonitoredItem);
                opcSubscription.ApplyChanges();

                // create a heartbeat timer, if required
                if (heartbeatInterval > 0)
                {
                    _heartbeats.Add(new HeartBeatPublishing((uint)heartbeatInterval, session, nodeId));
                }

                // create a skip first entry, if required
                if (skipFirst)
                {
                    MonitoredItemNotification.SkipFirst[nodeId.ToString()] = true;
                }

                Program.Instance.Logger.Information($"{logPrefix} Now monitoring OPC UA node {(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())} on endpoint {session.ConfiguredEndpoint.EndpointUrl}");
                Metrics.NumberOfOpcMonitoredItemsMonitored++;

                return HttpStatusCode.Accepted;
            }
            catch (ServiceResultException sre)
            {
                switch ((uint)sre.Result.StatusCode)
                {
                    case StatusCodes.BadSessionIdInvalid:
                        Program.Instance.Logger.Information($"Session with Id {session.SessionId} is no longer available on endpoint '{session.ConfiguredEndpoint.EndpointUrl}'. Cleaning up.");
                        return HttpStatusCode.Gone;

                    case StatusCodes.BadSubscriptionIdInvalid:
                        Program.Instance.Logger.Information($"Subscription with Id {opcSubscription.Id} is no longer available on endpoint '{session.ConfiguredEndpoint.EndpointUrl}'. Cleaning up.");
                        return HttpStatusCode.Gone;

                    case StatusCodes.BadNodeIdInvalid:
                    case StatusCodes.BadNodeIdUnknown:
                        Program.Instance.Logger.Error($"Failed to monitor node '{nodeId}' on endpoint '{session.ConfiguredEndpoint.EndpointUrl}'.");
                        Program.Instance.Logger.Error($"OPC UA ServiceResultException is '{sre.Result}'. Please check your publisher configuration for this node.");
                        return HttpStatusCode.InternalServerError;

                    default:
                        Program.Instance.Logger.Error($"Unhandled OPC UA ServiceResultException '{sre.Result}' when monitoring node '{nodeId}' on endpoint '{session.ConfiguredEndpoint.EndpointUrl}'. Continue.");
                        return HttpStatusCode.InternalServerError;
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, $"{logPrefix} Exception while trying to add node '{(expandedNodeId == null ? nodeId.ToString() : expandedNodeId.ToString())}' for monitoring.");
                return HttpStatusCode.InternalServerError;
            }
        }

        public HttpStatusCode UnpublishNode(NodePublishingConfigurationModel node)
        {
            // find the required session
            Session session = FindSession(node.EndpointUrl);
            if (session == null)
            {
                // session no longer exists
                return HttpStatusCode.Gone;
            }
                        
            // loop through all subscriptions of the session
            foreach (Subscription subscription in session.Subscriptions)
            {
                if (node.OpcPublishingInterval == subscription.PublishingInterval)
                {
                    // loop through all monitored items
                    foreach (MonitoredItem monitoredItem in subscription.MonitoredItems)
                    {
                        // make sure the node Id is valid
                        if ((node.NodeId == null) && (node.ExpandedNodeId != null))
                        {
                            node.NodeId = new NodeId(node.ExpandedNodeId.Identifier, node.ExpandedNodeId.NamespaceIndex);
                        }
                        if (monitoredItem.ResolvedNodeId == node.NodeId)
                        {
                            subscription.RemoveItem(monitoredItem);
                            subscription.ApplyChanges();
                            Metrics.NumberOfOpcMonitoredItemsMonitored--;

                            // cleanup empty subscriptions and sessions
                            if (subscription.MonitoredItemCount == 0)
                            {
                                session.RemoveSubscription(subscription);
                                Metrics.NumberOfOpcSubscriptionsConnected--;
                            }

                            return HttpStatusCode.OK;
                        }
                    }
                    break;
                }
            }

            // node no longer monitored 
            return HttpStatusCode.Gone;
        }

        public List<ConfigurationFileEntryModel> GetListofPublishedNodes()
        {
            List<ConfigurationFileEntryModel> publisherConfigurationFileEntries = new List<ConfigurationFileEntryModel>();
            
            try
            {
                // loop through all sessions
                lock (_sessionsLock)
                {
                    foreach (Session session in _sessions)
                    {
                        OpcUserSessionAuthenticationMode authenticationMode = OpcUserSessionAuthenticationMode.Anonymous;
                        EncryptedNetworkCredential credentials = null;
                        
                        if (session.Identity.TokenType == UserTokenType.UserName)
                        {
                            authenticationMode = OpcUserSessionAuthenticationMode.UsernamePassword;

                            UserNameIdentityToken token = (UserNameIdentityToken)session.Identity.GetIdentityToken();
                            string username = token.UserName;
                            string password = token.DecryptedPassword;
                            credentials = EncryptedNetworkCredential.Encrypt(_uaApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.LoadPrivateKey(null).Result, new NetworkCredential(username, password));
                        }
                       
                        ConfigurationFileEntryModel publisherConfigurationFileEntry = new ConfigurationFileEntryModel();
                        publisherConfigurationFileEntry.EndpointUrl = session.ConfiguredEndpoint.EndpointUrl;
                        publisherConfigurationFileEntry.OpcAuthenticationMode = authenticationMode;
                        publisherConfigurationFileEntry.EncryptedAuthCredential = credentials;
                        publisherConfigurationFileEntry.UseSecurity = SettingsConfiguration.UseSecurity;
                        publisherConfigurationFileEntry.OpcNodes = new List<OpcNodeOnEndpointModel>();

                        foreach (Subscription subscription in session.Subscriptions)
                        {
                            foreach (MonitoredItem monitoredItem in subscription.MonitoredItems)
                            {
                                OpcNodeOnEndpointModel opcNodeOnEndpoint = new OpcNodeOnEndpointModel(monitoredItem.ResolvedNodeId.ToString()) {
                                    OpcPublishingInterval = subscription.PublishingInterval,
                                    OpcSamplingInterval = monitoredItem.SamplingInterval,
                                    DisplayName = monitoredItem.DisplayName,
                                    HeartbeatInterval = 0, //TODO: Need to retrieve heartbeatinterval
                                    SkipFirst = false //TODO: Need to retrieve skipfirst flag
                                };
                                publisherConfigurationFileEntry.OpcNodes.Add(opcNodeOnEndpoint);
                            }
                        }

                        publisherConfigurationFileEntries.Add(publisherConfigurationFileEntry);
                    }
                }
            }
            catch (Exception e)
            {
                Program.Instance.Logger.Error(e, "Reading configuration file entries failed.");
                return null;
            }

            return publisherConfigurationFileEntries;
        }

        private ApplicationConfiguration _uaApplicationConfiguration;
        
        private List<Session> _sessions = new List<Session>();
        private object _sessionsLock = new object();

        private List<HeartBeatPublishing> _heartbeats = new List<HeartBeatPublishing>();

        private Dictionary<string, uint> _missedKeepAlives = new Dictionary<string, uint>();

        private Dictionary<string, EndpointDescription> _endpointDescriptionCache = new Dictionary<string, EndpointDescription>();
    }
}
