// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static OpcPublisher.Program;

namespace OpcPublisher.Configurations
{
    public class PublisherTelemetryConfiguration
    {
        public const string EndpointUrlNameDefault = "EndpointUrl";
        public const string NodeIdNameDefault = "NodeId";
        public const string ExpandedNodeIdNameDefault = "ExpandedNodeId";
        public const string ApplicationUriNameDefault = "ApplicationUri";
        public const string DisplayNameNameDefault = "DisplayName";
        public const string ValueNameDefault = "Value";
        public const string SourceTimestampNameDefault = "SourceTimestamp";
        public const string StatusNameDefault = "Status";
        public const string StatusCodeNameDefault = "StatusCode";

        public static string PublisherTelemetryConfigurationFilename { get; set; } = null;

        /// <summary>
        /// Get the singleton.
        /// </summary>
        public static PublisherTelemetryConfiguration Instance
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
                            _instance = new PublisherTelemetryConfiguration();
                        }
                        return _instance;
                    }
                }
            }
        }

        /// <summary>
        /// Ctor to initialize resources for the telemetry configuration.
        /// </summary>
        public PublisherTelemetryConfiguration()
        {
            _telemetryConfiguration = null;
            _endpointTelemetryConfigurations = new List<EndpointTelemetryConfigurationModel>();
            _defaultEndpointTelemetryConfiguration = null;
            _endpointTelemetryConfigurationCache = new Dictionary<string, EndpointTelemetryConfigurationModel>();

            // initialize with the default server telemetry configuration
            InitializePublisherDefaultEndpointTelemetryConfiguration();

            // read the configuration from the configuration file
            if (!ReadConfigAsync().Result)
            {
                string errorMessage = $"Error while reading the telemetry configuration file '{PublisherTelemetryConfigurationFilename}'";
                Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }
        }

        /// <summary>
        /// Method to get the telemetry configuration for a specific endpoint URL. If the endpoint URL is not found, then the default configuration is returned.
        /// </summary>
        public EndpointTelemetryConfigurationModel GetEndpointTelemetryConfiguration(string endpointUrl)
        {
            // lookup configuration in cache and return it or return default configuration
            if (_endpointTelemetryConfigurationCache.ContainsKey(endpointUrl))
            {
                return _endpointTelemetryConfigurationCache[endpointUrl];
            }
            return _defaultEndpointTelemetryConfiguration;
        }

        /// <summary>
        /// Validate the endpoint configuration. 'Name' and 'Flat' properties are not allowed and there is only one configuration per endpoint allowed.
        /// </summary>
        private bool ValidateEndpointConfiguration(EndpointTelemetryConfigurationModel config)
        {
            if (config.ForEndpointUrl == null)
            {
                Logger.Fatal("Each object in the 'EndpointSpecific' array must have a property 'ForEndpointUrl'. Please change.");
                return false;

            }
            if (_telemetryConfiguration.EndpointSpecific.Count(c => !string.IsNullOrEmpty(c.ForEndpointUrl) && c.ForEndpointUrl.Equals(config?.ForEndpointUrl, StringComparison.OrdinalIgnoreCase)) > 1)
            {
                Logger.Fatal($"The value '{config.ForEndpointUrl}' for property 'ForEndpointUrl' is only allowed to used once in the 'EndpointSpecific' array. Please change.");
                return false;
            }
            if (config.EndpointUrl.Name != null || config.NodeId.Name != null ||
                config.MonitoredItem.ApplicationUri.Name != null || config.MonitoredItem.DisplayName.Name != null ||
                config.Value.Value.Name != null || config.Value.SourceTimestamp.Name != null || config.Value.StatusCode.Name != null || config.Value.Status.Name != null)
            {
                Logger.Fatal("The property 'Name' is not allowed in any object in the 'EndpointSpecific' array. Please change.");
                return false;
            }
            if (config.MonitoredItem.Flat != null || config.Value.Flat != null)
            {
                Logger.Fatal("The property 'Flat' is not allowed in any object in the 'EndpointSpecific' array. Please change.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Initialize the default configuration to be compatible with Connected factory Preconfigured Solution.
        /// </summary>
        private void InitializePublisherDefaultEndpointTelemetryConfiguration()
        {
            // create the default configuration
            _defaultEndpointTelemetryConfiguration = new EndpointTelemetryConfigurationModel();

            // set defaults for 'Name' to be compatible with Connected factory
            _defaultEndpointTelemetryConfiguration.EndpointUrl.Name = EndpointUrlNameDefault;
            _defaultEndpointTelemetryConfiguration.NodeId.Name = NodeIdNameDefault;
            _defaultEndpointTelemetryConfiguration.ExpandedNodeId.Name = ExpandedNodeIdNameDefault;
            _defaultEndpointTelemetryConfiguration.MonitoredItem.ApplicationUri.Name = ApplicationUriNameDefault;
            _defaultEndpointTelemetryConfiguration.MonitoredItem.DisplayName.Name = DisplayNameNameDefault;
            _defaultEndpointTelemetryConfiguration.Value.Value.Name = ValueNameDefault;
            _defaultEndpointTelemetryConfiguration.Value.SourceTimestamp.Name = SourceTimestampNameDefault;
            _defaultEndpointTelemetryConfiguration.Value.StatusCode.Name = StatusCodeNameDefault;
            _defaultEndpointTelemetryConfiguration.Value.Status.Name = StatusCodeNameDefault;

            // set defaults for 'Publish' to be compatible with Connected factory
            _defaultEndpointTelemetryConfiguration.EndpointUrl.Publish = false;
            _defaultEndpointTelemetryConfiguration.NodeId.Publish = true;
            _defaultEndpointTelemetryConfiguration.ExpandedNodeId.Publish = false;
            _defaultEndpointTelemetryConfiguration.MonitoredItem.ApplicationUri.Publish = true;
            _defaultEndpointTelemetryConfiguration.MonitoredItem.DisplayName.Publish = true;
            _defaultEndpointTelemetryConfiguration.Value.Value.Publish = true;
            _defaultEndpointTelemetryConfiguration.Value.SourceTimestamp.Publish = true;
            _defaultEndpointTelemetryConfiguration.Value.StatusCode.Publish = false;
            _defaultEndpointTelemetryConfiguration.Value.Status.Publish = false;

            // set defaults for 'Flat' to be compatible with Connected factory
            _defaultEndpointTelemetryConfiguration.MonitoredItem.Flat = true;
            _defaultEndpointTelemetryConfiguration.Value.Flat = false;

            // 'Pattern' is set to null on creation which is whats default
        }

        /// <summary>
        /// Update the default configuration with the settings give in the 'Defaults' object of the configuration file.
        /// </summary>
        public bool UpdateDefaultEndpointTelemetryConfiguration()
        {
            // sanity check user default configuration
            if (_telemetryConfiguration.Defaults != null)
            {
                if (_telemetryConfiguration.Defaults.ForEndpointUrl != null)
                {
                    Logger.Fatal("The property 'ForEndpointUrl' is not allowed in 'Defaults'. Please change.");
                    return false;
                }

                // process all properties
                _defaultEndpointTelemetryConfiguration.EndpointUrl = _telemetryConfiguration.Defaults.EndpointUrl;
                _defaultEndpointTelemetryConfiguration.NodeId = _telemetryConfiguration.Defaults.NodeId;
                _defaultEndpointTelemetryConfiguration.ExpandedNodeId = _telemetryConfiguration.Defaults.ExpandedNodeId;
                _defaultEndpointTelemetryConfiguration.MonitoredItem = _telemetryConfiguration.Defaults.MonitoredItem;
                _defaultEndpointTelemetryConfiguration.Value = _telemetryConfiguration.Defaults.Value;
            }
            return true;
        }

        /// <summary>
        /// Update the endpoint specific telemetry configuration using settings from the default configuration.
        /// Only those settings are applied, which are not defined by the endpoint specific configuration.
        /// </summary>
        public void UpdateEndpointTelemetryConfiguration(EndpointTelemetryConfigurationModel config)
        {
            // process all properties, applying only those defaults which are not set in the endpoint specific config
            config.EndpointUrl.Name = config.EndpointUrl.Name ?? _defaultEndpointTelemetryConfiguration.EndpointUrl.Name;
            config.EndpointUrl.Publish = config.EndpointUrl.Publish ?? _defaultEndpointTelemetryConfiguration.EndpointUrl.Publish;
            config.EndpointUrl.Pattern = config.EndpointUrl.Pattern ?? _defaultEndpointTelemetryConfiguration.EndpointUrl.Pattern;

            config.NodeId.Name = config.NodeId.Name ?? _defaultEndpointTelemetryConfiguration.NodeId.Name;
            config.NodeId.Publish = config.NodeId.Publish ?? _defaultEndpointTelemetryConfiguration.NodeId.Publish;
            config.NodeId.Pattern = config.NodeId.Pattern ?? _defaultEndpointTelemetryConfiguration.NodeId.Pattern;

            config.ExpandedNodeId.Name = config.ExpandedNodeId.Name ?? _defaultEndpointTelemetryConfiguration.ExpandedNodeId.Name;
            config.ExpandedNodeId.Publish = config.ExpandedNodeId.Publish ?? _defaultEndpointTelemetryConfiguration.ExpandedNodeId.Publish;
            config.ExpandedNodeId.Pattern = config.ExpandedNodeId.Pattern ?? _defaultEndpointTelemetryConfiguration.ExpandedNodeId.Pattern;

            config.MonitoredItem.Flat = config.MonitoredItem.Flat ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.Flat;

            config.MonitoredItem.ApplicationUri.Name = config.MonitoredItem.ApplicationUri.Name ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.ApplicationUri.Name;
            config.MonitoredItem.ApplicationUri.Publish = config.MonitoredItem.ApplicationUri.Publish ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.ApplicationUri.Publish;
            config.MonitoredItem.ApplicationUri.Pattern = config.MonitoredItem.ApplicationUri.Pattern ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.ApplicationUri.Pattern;

            config.MonitoredItem.DisplayName.Name = config.MonitoredItem.DisplayName.Name ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.DisplayName.Name;
            config.MonitoredItem.DisplayName.Publish = config.MonitoredItem.DisplayName.Publish ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.DisplayName.Publish;
            config.MonitoredItem.DisplayName.Pattern = config.MonitoredItem.DisplayName.Pattern ?? _defaultEndpointTelemetryConfiguration.MonitoredItem.DisplayName.Pattern;

            config.Value.Flat = config.Value.Flat ?? _defaultEndpointTelemetryConfiguration.Value.Flat;

            config.Value.Value.Name = config.Value.Value.Name ?? _defaultEndpointTelemetryConfiguration.Value.Value.Name;
            config.Value.Value.Publish = config.Value.Value.Publish ?? _defaultEndpointTelemetryConfiguration.Value.Value.Publish;
            config.Value.Value.Pattern = config.Value.Value.Pattern ?? _defaultEndpointTelemetryConfiguration.Value.Value.Pattern;

            config.Value.SourceTimestamp.Name = config.Value.SourceTimestamp.Name ?? _defaultEndpointTelemetryConfiguration.Value.SourceTimestamp.Name;
            config.Value.SourceTimestamp.Publish = config.Value.SourceTimestamp.Publish ?? _defaultEndpointTelemetryConfiguration.Value.SourceTimestamp.Publish;
            config.Value.SourceTimestamp.Pattern = config.Value.SourceTimestamp.Pattern ?? _defaultEndpointTelemetryConfiguration.Value.SourceTimestamp.Pattern;

            config.Value.StatusCode.Name = config.Value.StatusCode.Name ?? _defaultEndpointTelemetryConfiguration.Value.StatusCode.Name;
            config.Value.StatusCode.Publish = config.Value.StatusCode.Publish ?? _defaultEndpointTelemetryConfiguration.Value.StatusCode.Publish;
            config.Value.StatusCode.Pattern = config.Value.StatusCode.Pattern ?? _defaultEndpointTelemetryConfiguration.Value.StatusCode.Pattern;

            config.Value.Status.Name = config.Value.Status.Name ?? _defaultEndpointTelemetryConfiguration.Value.Status.Name;
            config.Value.Status.Publish = config.Value.Status.Publish ?? _defaultEndpointTelemetryConfiguration.Value.Status.Publish;
            config.Value.Status.Pattern = config.Value.Status.Pattern ?? _defaultEndpointTelemetryConfiguration.Value.Status.Pattern;
        }

        /// <summary>
        /// Read and parse the publisher telemetry configuration file.
        /// </summary>
        private async Task<bool> ReadConfigAsync()
        {
            // return if there is no configuration file specified
            if (string.IsNullOrEmpty(PublisherTelemetryConfigurationFilename))
            {
                Logger.Information("Using default telemetry configuration.");
                return true;
            }

            // get information on the telemetry configuration
            try
            {
                Logger.Information($"Attempting to load telemetry configuration file from: {PublisherTelemetryConfigurationFilename}");
                _telemetryConfiguration = JsonConvert.DeserializeObject<TelemetryConfigurationFileModel>(await File.ReadAllTextAsync(PublisherTelemetryConfigurationFilename).ConfigureAwait(false));

                // update the default configuration with the 'Defaults' settings from the configuration file
                if (UpdateDefaultEndpointTelemetryConfiguration() == false)
                {
                    return false;
                }

                // sanity check all endpoint specific configurations and add them to the lookup dictionary
                foreach (var config in _telemetryConfiguration.EndpointSpecific)
                {
                    // validate the endpoint specific telemetry configuration
                    if (ValidateEndpointConfiguration(config) == false)
                    {
                        return false;
                    }

                    // set defaults for unset values
                    UpdateEndpointTelemetryConfiguration(config);

                    // add the endpoint configuration to the lookup cache
                    _endpointTelemetryConfigurationCache.Add(config.ForEndpointUrl, config);
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Loading of the telemetry configuration file failed. Does the file exist and has correct syntax? Exiting...");
                return false;
            }
            return true;
        }

        private TelemetryConfigurationFileModel _telemetryConfiguration;
        private readonly List<EndpointTelemetryConfigurationModel> _endpointTelemetryConfigurations;
        private EndpointTelemetryConfigurationModel _defaultEndpointTelemetryConfiguration;
        private readonly Dictionary<string, EndpointTelemetryConfigurationModel> _endpointTelemetryConfigurationCache;

        private static readonly object _singletonLock = new object();
        private static PublisherTelemetryConfiguration _instance = null;
    }
}
