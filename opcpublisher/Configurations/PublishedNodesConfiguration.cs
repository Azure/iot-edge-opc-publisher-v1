// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace OpcPublisher.Configurations
{
    public class PublishedNodesConfiguration
    {
        /// <summary>
        /// Keeps the version of the node configuration that has lastly been persisted
        /// </summary>
        private static uint _lastNodeConfigVersion = 0;

        /// <summary>
        /// Read and parse the publisher node configuration file.
        /// </summary>
        /// <returns></returns>
        public static bool ReadConfig(UAClient client, X509Certificate2 cert)
        {
            // get information on the nodes to publish and validate the json by deserializing it.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("_GW_PNFP")))
            {
                Program.Instance.Logger.Information("Publishing node configuration file path read from environment.");
                SettingsConfiguration.PublisherNodeConfigurationFilename = Environment.GetEnvironmentVariable("_GW_PNFP");
            }
            Program.Instance.Logger.Information($"The name of the configuration file for published nodes is: {SettingsConfiguration.PublisherNodeConfigurationFilename}");

            // if the file exists, read it, if not just continue 
            if (File.Exists(SettingsConfiguration.PublisherNodeConfigurationFilename))
            {
                List<ConfigurationFileEntryLegacyModel> _configurationFileEntries = null;
                Program.Instance.Logger.Information($"Attempting to load node configuration from: {SettingsConfiguration.PublisherNodeConfigurationFilename}");
                try
                {
                    string json = File.ReadAllText(SettingsConfiguration.PublisherNodeConfigurationFilename);
                    _configurationFileEntries = JsonConvert.DeserializeObject<List<ConfigurationFileEntryLegacyModel>>(json);
                }
                catch (Exception ex)
                {
                    Program.Instance.Logger.Error(ex, $"Loading of the node configuration file failed with {ex.Message}. Does the file exist and does it have the correct syntax?");
                }
                                       
                if (_configurationFileEntries != null)
                {
                    Program.Instance.Logger.Information($"Loaded {_configurationFileEntries.Count} config file entry/entries.");
                    foreach (ConfigurationFileEntryLegacyModel publisherConfigFileEntryLegacy in _configurationFileEntries)
                    {
                        // decrypt username and password, if required
                        NetworkCredential decryptedCreds = null;
                        if (publisherConfigFileEntryLegacy.EncryptedAuthCredential != null)
                        {
                            decryptedCreds = publisherConfigFileEntryLegacy.EncryptedAuthCredential.Decrypt(cert);
                        }

                        if (publisherConfigFileEntryLegacy.NodeId == null)
                        {
                            // new node configuration syntax.
                            foreach (OpcNodeOnEndpointModel opcNode in publisherConfigFileEntryLegacy.OpcNodes)
                            {
                                if (opcNode.ExpandedNodeId != null)
                                {
                                    ExpandedNodeId expandedNodeId = ExpandedNodeId.Parse(opcNode.ExpandedNodeId);
                                    NodePublishingConfigurationModel publishingInfo = new NodePublishingConfigurationModel() {
                                        ExpandedNodeId = expandedNodeId,
                                        NodeId = opcNode.ExpandedNodeId,
                                        EndpointUrl = publisherConfigFileEntryLegacy.EndpointUrl.OriginalString,
                                        UseSecurity = (bool)publisherConfigFileEntryLegacy.UseSecurity,
                                        OpcPublishingInterval = opcNode.OpcPublishingInterval,
                                        OpcSamplingInterval = opcNode.OpcSamplingInterval,
                                        DisplayName = opcNode.DisplayName,
                                        HeartbeatInterval = opcNode.HeartbeatInterval,
                                        SkipFirst = opcNode.SkipFirst,
                                        OpcAuthenticationMode = publisherConfigFileEntryLegacy.OpcAuthenticationMode,
                                        AuthCredential = decryptedCreds
                                    };
                                    client.PublishNode(publishingInfo);
                                }
                                else
                                {
                                    // check Id string to check which format we have
                                    if (opcNode.Id.StartsWith("nsu=", StringComparison.InvariantCulture))
                                    {
                                        // ExpandedNodeId format
                                        ExpandedNodeId expandedNodeId = ExpandedNodeId.Parse(opcNode.Id);
                                        NodePublishingConfigurationModel publishingInfo = new NodePublishingConfigurationModel() {
                                            ExpandedNodeId = expandedNodeId,
                                            NodeId = opcNode.Id,
                                            EndpointUrl = publisherConfigFileEntryLegacy.EndpointUrl.OriginalString,
                                            UseSecurity = (bool)publisherConfigFileEntryLegacy.UseSecurity,
                                            OpcPublishingInterval = opcNode.OpcPublishingInterval,
                                            OpcSamplingInterval = opcNode.OpcSamplingInterval,
                                            DisplayName = opcNode.DisplayName,
                                            HeartbeatInterval = opcNode.HeartbeatInterval,
                                            SkipFirst = opcNode.SkipFirst,
                                            OpcAuthenticationMode = publisherConfigFileEntryLegacy.OpcAuthenticationMode,
                                            AuthCredential = decryptedCreds
                                        };
                                        client.PublishNode(publishingInfo);
                                    }
                                    else
                                    {
                                        // NodeId format
                                        NodeId nodeId = NodeId.Parse(opcNode.Id);
                                        NodePublishingConfigurationModel publishingInfo = new NodePublishingConfigurationModel {
                                            ExpandedNodeId = nodeId,
                                            NodeId = opcNode.Id,
                                            EndpointUrl = publisherConfigFileEntryLegacy.EndpointUrl.OriginalString,
                                            UseSecurity = (bool)publisherConfigFileEntryLegacy.UseSecurity,
                                            OpcPublishingInterval = opcNode.OpcPublishingInterval,
                                            OpcSamplingInterval = opcNode.OpcSamplingInterval,
                                            DisplayName = opcNode.DisplayName,
                                            HeartbeatInterval = opcNode.HeartbeatInterval,
                                            SkipFirst = opcNode.SkipFirst,
                                            OpcAuthenticationMode = publisherConfigFileEntryLegacy.OpcAuthenticationMode,
                                            AuthCredential = decryptedCreds
                                        };
                                       client.PublishNode(publishingInfo);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // NodeId (ns=) format node configuration syntax using default sampling and publishing interval.
                            NodePublishingConfigurationModel publishingInfo = new NodePublishingConfigurationModel() {
                                ExpandedNodeId = publisherConfigFileEntryLegacy.NodeId,
                                NodeId = publisherConfigFileEntryLegacy.NodeId.ToString(),
                                EndpointUrl = publisherConfigFileEntryLegacy.EndpointUrl.OriginalString,
                                UseSecurity = (bool)publisherConfigFileEntryLegacy.UseSecurity,
                                OpcAuthenticationMode = publisherConfigFileEntryLegacy.OpcAuthenticationMode,
                                AuthCredential = decryptedCreds
                            };
                            client.PublishNode(publishingInfo);
                        }
                    }
                }
            }
            else
            {
                Program.Instance.Logger.Information($"The node configuration file '{SettingsConfiguration.PublisherNodeConfigurationFilename}' does not exist. Continue and wait for remote configuration requests.");
            }

            return true;
        }
      
        /// <summary>
        /// Updates the configuration file to persist all currently published nodes
        /// </summary>
        public static async Task UpdateNodeConfigurationFileAsync(UAClient client, uint nodeConfigVersion)
        {
            // only persist newer versions
            if (nodeConfigVersion > _lastNodeConfigVersion)
            {
                try
                {
                    // iterate through all sessions, subscriptions and monitored items and create config file entries
                    List<ConfigurationFileEntryModel> publisherNodeConfiguration = client.GetListofPublishedNodes();

                    Program.Instance.Logger.Debug($"Update node configuration file, version: {_lastNodeConfigVersion:X8}");

                    // update the config file
                    await File.WriteAllTextAsync(SettingsConfiguration.PublisherNodeConfigurationFilename, JsonConvert.SerializeObject(publisherNodeConfiguration, Formatting.Indented)).ConfigureAwait(false);

                    _lastNodeConfigVersion = nodeConfigVersion;
                }
                catch (Exception e)
                {
                    Program.Instance.Logger.Error(e, "Update of node configuration file failed.");
                }
            }
        }
    }
}
