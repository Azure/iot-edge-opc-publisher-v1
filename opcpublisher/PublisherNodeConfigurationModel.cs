using Newtonsoft.Json;
using Opc.Ua;
using System;
using System.Collections.Generic;

namespace OpcPublisher
{
    using System.ComponentModel;

    /// <summary>
    /// Class describing a list of nodes
    /// </summary>
    public class OpcNodeOnEndpointModel
    {
        public OpcNodeOnEndpointModel(string id, string expandedNodeId = null, int? opcSamplingInterval = null, int? opcPublishingInterval = null,
            string displayName = null, int? heartbeatInterval = null, bool? skipFirst = null)
        {
            Id = id;
            ExpandedNodeId = expandedNodeId;
            OpcSamplingInterval = opcSamplingInterval;
            OpcPublishingInterval = opcPublishingInterval;
            DisplayName = displayName;
            HeartbeatInterval = heartbeatInterval;
            SkipFirst = skipFirst;
        }

        // Id can be:
        // a NodeId ("ns=")
        // an ExpandedNodeId ("nsu=")
        public string Id { get; set; }

        // support legacy configuration file syntax
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExpandedNodeId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? OpcSamplingInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? OpcPublishingInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DisplayName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? HeartbeatInterval { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? SkipFirst { get; set; }
    }

    /// <summary>
    /// Class describing the nodes which should be published.
    /// </summary>
    public partial class PublisherConfigurationFileEntryModel
    {
        public PublisherConfigurationFileEntryModel()
        {
        }

        public PublisherConfigurationFileEntryModel(string endpointUrl)
        {
            EndpointUrl = new Uri(endpointUrl);
            OpcNodes = new List<OpcNodeOnEndpointModel>();
        }

        public Uri EndpointUrl { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public bool? UseSecurity { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
#pragma warning disable CA2227 // Collection properties should be read only
        public List<OpcNodeOnEndpointModel> OpcNodes { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
#pragma warning disable CA2227 // Collection properties should be read only
        public List<OpcEventOnEndpointModel> OpcEvents { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }

    /// <summary>
    /// Describes the publishing information of a node.
    /// </summary>
    public class NodePublishingConfigurationModel
    {
        /// <summary>
        /// The endpoint URL of the OPC UA server.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// Flag if a secure transport should be used to connect to the endpoint.
        /// </summary>
        public bool UseSecurity { get; set; }

        /// <summary>
        /// The node to monitor in "ns=" syntax.
        /// </summary>
        public NodeId NodeId { get; set; }

        /// <summary>
        /// The node to monitor in "nsu=" syntax.
        /// </summary>
        public ExpandedNodeId ExpandedNodeId { get; set; }

        /// <summary>
        /// The Id of the node as it was configured.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The display name to use for the node in telemetry events.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The OPC UA sampling interval for the node.
        /// </summary>
        public int? OpcSamplingInterval { get; set; }

        /// <summary>
        /// The OPC UA publishing interval for the node.
        /// </summary>
        public int? OpcPublishingInterval { get; set; }

        /// <summary>
        /// Flag to enable a hardbeat telemetry event publish for the node.
        /// </summary>
        public int? HeartbeatInterval { get; set; }

        /// <summary>
        /// Flag to skip the first telemetry event for the node after connect.
        /// </summary>
        public bool? SkipFirst { get; set; }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public NodePublishingConfigurationModel(ExpandedNodeId expandedNodeId, string id, string endpointUrl, bool? useSecurity,
                    int? opcPublishingInterval, int? opcSamplingInterval, string displayName, int? heartbeatInterval, bool? skipFirst)

        {
            NodeId = null;
            ExpandedNodeId = expandedNodeId;
            Id = id;
            EndpointUrl = endpointUrl;
            UseSecurity = useSecurity ?? true;
            DisplayName = displayName;
            OpcSamplingInterval = opcSamplingInterval;
            OpcPublishingInterval = opcPublishingInterval;
            HeartbeatInterval = heartbeatInterval;
            SkipFirst = skipFirst;
        }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public NodePublishingConfigurationModel(NodeId nodeId, string id, string endpointUrl, bool? useSecurity,
                    int? opcPublishingInterval, int? opcSamplingInterval, string displayName, int? heartbeatInterval, bool? skipFirst)
        {
            NodeId = nodeId;
            ExpandedNodeId = null;
            Id = id;
            EndpointUrl = endpointUrl;
            UseSecurity = useSecurity ?? true;
            DisplayName = displayName;
            OpcSamplingInterval = opcSamplingInterval;
            OpcPublishingInterval = opcPublishingInterval;
            HeartbeatInterval = heartbeatInterval;
            SkipFirst = skipFirst;
        }
    }

    /// <summary>
    /// Describes the information of an event.
    /// </summary>
    public class EventConfigurationModel
    {
        /// <summary>
        /// The endpoint URL of the OPC UA server.
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// Flag if a secure transport should be used to connect to the endpoint.
        /// </summary>
        public bool UseSecurity { get; set; }

        /// <summary>
        /// The event source to monitor.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The display name to use for the node in telemetry events.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The select clauses of the event.
        /// </summary>
        public List<SelectClause> SelectClauses { get; set; }

        /// <summary>
        /// The where clauses of the event.
        /// </summary>
        public List<WhereClauseElement> WhereClause { get; set; }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public EventConfigurationModel(string endpointUrl, bool? useSecurity,
            string id, string displayName, List<SelectClause> selectClauses, List<WhereClauseElement> whereClause)
        {
            EndpointUrl = endpointUrl;
            UseSecurity = useSecurity ?? true;
            Id = id;
            DisplayName = displayName;
            SelectClauses = selectClauses;
            WhereClause = whereClause;
        }
    }


    /// <summary>
    /// Class describing the nodes which should be published. It supports two formats:
    /// - NodeId syntax using the namespace index (ns) syntax. This is only used in legacy environments and is only supported for backward compatibility.
    /// - List of ExpandedNodeId syntax, to allow putting nodes with similar publishing and/or sampling intervals in one object
    /// </summary>
    public partial class PublisherConfigurationFileEntryLegacyModel
    {
        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public PublisherConfigurationFileEntryLegacyModel()
        {
            OpcNodes = new List<OpcNodeOnEndpointModel>();
            OpcEvents = new List<OpcEventOnEndpointModel>();
        }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public PublisherConfigurationFileEntryLegacyModel(string nodeId, string endpointUrl)
        {
            NodeId = new NodeId(nodeId);
            EndpointUrl = new Uri(endpointUrl);
            OpcNodes = new List<OpcNodeOnEndpointModel>();
            OpcEvents = new List<OpcEventOnEndpointModel>();
        }

        /// <summary>
        /// The endpoint URL of the OPC UA server.
        /// </summary>
        public Uri EndpointUrl { get; set; }

        /// <summary>
        /// Flag if a secure transport should be used to connect to the endpoint.
        /// </summary>
        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public bool? UseSecurity { get; set; }

        /// <summary>
        /// The node to monitor in "ns=" syntax. This key is only supported for backward compatibility and should not be used anymore.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public NodeId NodeId { get; set; }

        /// <summary>
        /// Instead all nodes should be defined in this collection.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
#pragma warning disable CA2227 // Collection properties should be read only
        public List<OpcNodeOnEndpointModel> OpcNodes { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        /// <summary>
        /// Event and Conditions are defined here.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
#pragma warning disable CA2227 // Collection properties should be read only
        public List<OpcEventOnEndpointModel> OpcEvents { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }

    /// <summary>
    /// Class describing select clauses for an event filter.
    /// </summary>
    public class SelectClause
    {
        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public SelectClause(string typeId, List<string> browsePaths, string attributeId, string indexRange)
        {
            TypeId = typeId;
            BrowsePaths = browsePaths;

            AttributeId = attributeId;
            attributeId.ResolveAttributeId();

            IndexRange = indexRange;
            indexRange.ResolveIndexRange();
        }

        /// <summary>
        /// The NodeId of the SimpleAttributeOperand.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string TypeId;

        /// <summary>
        /// A list of QualifiedName's describing the field to be published.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<string> BrowsePaths;

        /// <summary>
        /// The Attribute of the identified node to be published. This is Value by default.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AttributeId;

        /// <summary>
        /// The index range of the node values to be published.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string IndexRange;
    }

    /// <summary>
    /// Class to describe the AttributeOperand.
    /// </summary>
    public class FilterAttribute
    {
        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public FilterAttribute(string nodeId, string alias, string browsePath, string attributeId, string indexRange)
        {
            NodeId = nodeId;
            BrowsePath = browsePath;
            Alias = alias;

            AttributeId = attributeId;
            attributeId.ResolveAttributeId();

            IndexRange = indexRange;
            indexRange.ResolveIndexRange();
        }

        /// <summary>
        /// The NodeId of the AttributeOperand.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string NodeId;

        /// <summary>
        /// The Alias of the AttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Alias;

        /// <summary>
        /// A RelativePath describing the browse path from NodeId of the AttributeOperand.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string BrowsePath;

        /// <summary>
        /// The AttibuteId of the AttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AttributeId;

        /// <summary>
        /// The IndexRange of the AttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string IndexRange;
    }

    /// <summary>
    /// Class to describe the SimpleAttributeOperand.
    /// </summary>
    public class FilterSimpleAttribute
    {
        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public FilterSimpleAttribute(string typeId, List<string> browsePath, string attributeId, string indexRange)
        {
            TypeId = typeId;
            BrowsePaths = browsePath;

            AttributeId = attributeId;
            attributeId.ResolveAttributeId();

            IndexRange = indexRange;
            indexRange.ResolveIndexRange();
        }

        /// <summary>
        /// The TypeId of the SimpleAttributeOperand.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string TypeId;

        /// <summary>
        /// The browse path as a list of QualifiedName's of the SimpleAttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> BrowsePaths;

        /// <summary>
        /// The AttributeId of the SimpleAttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AttributeId;

        /// <summary>
        /// The IndexRange of the SimpleAttributeOperand.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string IndexRange;
    }

    /// <summary>
    /// Class to describe an operand of an WhereClauseElement.
    /// </summary>
    public class WhereClauseOperand
    {
        /// <summary>
        /// Holds an element value.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore, NullValueHandling = NullValueHandling.Ignore)]
        public uint? Element;

        /// <summary>
        /// Holds an Literal value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Literal;

        /// <summary>
        /// Holds an AttributeOperand value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterAttribute Attribute;

        /// <summary>
        /// Holds an SimpleAttributeOperand value.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FilterSimpleAttribute SimpleAttribute;

        /// <summary>
        /// Fetches the operand value.
        /// </summary>
        public FilterOperand GetOperand(TypeInfo typeInfo)
        {
            if (Element != null)
            {
                return new ElementOperand((uint)Element);
            }
            if (Literal != null)
            {
                object targetLiteral = TypeInfo.Cast(Literal, typeInfo.BuiltInType);
                return new LiteralOperand(targetLiteral);
            }
            if (Attribute != null)
            {
                AttributeOperand attributeOperand = new AttributeOperand(new NodeId(Attribute.NodeId), Attribute.BrowsePath);
                attributeOperand.Alias = Attribute.Alias;
                attributeOperand.AttributeId = Attribute.AttributeId.ResolveAttributeId();
                attributeOperand.IndexRange = Attribute.IndexRange;
                return attributeOperand;
            }
            if (SimpleAttribute != null)
            {
                List<QualifiedName> browsePaths = new List<QualifiedName>();
                if (SimpleAttribute.BrowsePaths != null)
                {
                    foreach (var browsePath in SimpleAttribute.BrowsePaths)
                    {
                        browsePaths.Add(new QualifiedName(browsePath));
                    }
                }
                SimpleAttributeOperand simpleAttributeOperand = new SimpleAttributeOperand(new NodeId(SimpleAttribute.TypeId), browsePaths.ToArray());
                simpleAttributeOperand.AttributeId = SimpleAttribute.AttributeId.ResolveAttributeId();
                simpleAttributeOperand.IndexRange = SimpleAttribute.IndexRange;
                return simpleAttributeOperand;
            }
            return null;
        }
    }

    /// <summary>
    /// Class describing where clauses for an event filter.
    /// </summary>
    public class WhereClauseElement
    {
        /// <summary>
        /// Ctor of an object.
        /// </summary>
        public WhereClauseElement()
        {
            Operands = new List<WhereClauseOperand>();
        }

        /// <summary>
        /// Ctor of an object using the given operator and operands.
        /// </summary>
        public WhereClauseElement(string op, List<WhereClauseOperand> operands)
        {
            op.ResolveFilterOperator();
            Operands = operands;
        }

        /// <summary>
        /// The Operator of the WhereClauseElement.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Operator;

        /// <summary>
        /// The Operands of the WhereClauseElement.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<WhereClauseOperand> Operands;
    }


    /// <summary>
    /// Class describing a list of events and fields to publish.
    /// </summary>
    public class OpcEventOnEndpointModel
    {
        /// <summary>
        /// The event source of the event. This is a NodeId, which has the SubscribeToEvents bit set in the EventNotifier attribute.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string Id;

        /// <summary>
        /// A display name which can be added when publishing the event information.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DisplayName;

        /// <summary>
        /// The SelectClauses used to select the fields which should be published for an event.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<SelectClause> SelectClauses;

        /// <summary>
        /// The WhereClause to specify which events are of interest.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<WhereClauseElement> WhereClause;

        /// <summary>
        /// Ctor of an object.
        /// </summary>
        public OpcEventOnEndpointModel()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            SelectClauses = new List<SelectClause>();
            WhereClause = new List<WhereClauseElement>();
        }

        /// <summary>
        /// Ctor of an object using a configuration object.
        /// </summary>
        public OpcEventOnEndpointModel(EventConfigurationModel eventConfiguration)
        {
            Id = eventConfiguration.Id;
            DisplayName = eventConfiguration.DisplayName;
            SelectClauses = eventConfiguration.SelectClauses;
            WhereClause = eventConfiguration.WhereClause;
        }

    }
}
