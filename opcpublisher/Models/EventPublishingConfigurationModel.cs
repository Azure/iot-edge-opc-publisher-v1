// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Opc.Ua;
using System.Collections.Generic;

namespace OpcPublisher
{
    /// <summary>
    /// Describes the information of an event.
    /// </summary>
    public class EventPublishingConfigurationModel : NodePublishingConfigurationModel
    {
        /// <summary>
        /// The select clauses of the event.
        /// </summary>
        public List<SelectClause> SelectClauses { get; set; }

        /// <summary>
        /// The where clauses of the event.
        /// </summary>
        public List<WhereClauseElement> WhereClauses { get; set; }
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
                    foreach (string browsePath in SimpleAttribute.BrowsePaths)
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
        public List<WhereClauseElement> WhereClauses;

        /// <summary>
        /// Ctor of an object.
        /// </summary>
        public OpcEventOnEndpointModel()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            SelectClauses = new List<SelectClause>();
            WhereClauses = new List<WhereClauseElement>();
        }

        /// <summary>
        /// Ctor of an object using a configuration object.
        /// </summary>
        public OpcEventOnEndpointModel(EventPublishingConfigurationModel eventConfiguration)
        {
            Id = eventConfiguration.NodeId.ToString();
            DisplayName = eventConfiguration.DisplayName;
            SelectClauses = eventConfiguration.SelectClauses;
            WhereClauses = eventConfiguration.WhereClauses;
        }
    }
}
