// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Opc.Ua;
using System;

namespace OpcPublisher
{
    public static class StringEx
    {
        public static uint ResolveAttributeId(this string attributeId)
        {
            uint resolvedAttributeId = Attributes.Value;
            if (!string.IsNullOrEmpty(attributeId))
            {
                if (uint.TryParse(attributeId, out resolvedAttributeId))
                {
                    resolvedAttributeId = uint.Parse(attributeId);
                }
                else
                {
                    if ((resolvedAttributeId = Attributes.GetIdentifier(attributeId)) == 0)
                    {
                        string errorMessage = $"The given Attribute '{attributeId}' in a select clause is not an valid attribute identifier.";
                        Program.Instance.Logger.Error(errorMessage);
                        throw new Exception(errorMessage);
                    }
                }
            }
            return resolvedAttributeId;
        }

        public static NumericRange ResolveIndexRange(this string indexRange)
        {
            NumericRange resolvedIndexRange = new NumericRange(0, 0);
            if (!string.IsNullOrEmpty(indexRange))
            {
                try
                {
                    resolvedIndexRange = NumericRange.Parse(indexRange);
                }
                catch (Exception e)
                {
                    string errorMessage = $"The given IndexRange '{indexRange}' in a select clause has not a valid syntax.";
                    Program.Instance.Logger.Error(e, errorMessage);
                    throw new Exception(errorMessage);
                }
            }
            return resolvedIndexRange;
        }

        public static FilterOperator ResolveFilterOperator(this string filterOperator)
        {

            if (Enum.TryParse(filterOperator, out FilterOperator resolvedFilterOperator) == false)
            {
                string errorMessage = $"The given filter operator '{filterOperator}' in a where clause has not a valid syntax.";
                Program.Instance.Logger.Error(errorMessage);
                throw new Exception(errorMessage);
            }
            return resolvedFilterOperator;
        }

        public static NodeId ToNodeId(this string id, NamespaceTable namespaceTable)
        {
            NodeId nodeId = null;
            if (id.Contains("nsu=", StringComparison.InvariantCulture))
            {
                ExpandedNodeId expandedNodeId = ExpandedNodeId.Parse(id);
                int namespaceIndex = namespaceTable.GetIndex(expandedNodeId.NamespaceUri);
                if (namespaceIndex >= 0)
                {
                    nodeId = new NodeId(expandedNodeId.Identifier, (ushort)namespaceIndex);
                }
                else
                {
                    string errorMessage = $"The given id can not converted to a NodeId '{id}', because the namespace URI '{expandedNodeId.NamespaceUri}' is unknown.";
                    Program.Instance.Logger.Error(errorMessage);
                }
            }
            else
            {
                nodeId = NodeId.Parse(id);
            }
            return nodeId;
        }
    }
}
