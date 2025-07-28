// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.PubSub
{
    public class HierarchicalNode
    {
        public HierarchicalNode? Parent { get; set; }
        public NodeId NodeId { get; set; }
        public ExpandedNodeId TypeId { get; set; }
        public QualifiedName BrowseName { get; set; }
        public LocalizedText DisplayName { get; set; } = "";
        public LocalizedText Description { get; set; } = "";
        public ExpandedNodeId TypeDefinitionNodeId { get; set; } = "";

        public FieldMetaData? fieldMetaData = null;
        public TypeDefinitionNode? TypeDefinitionNode { get; set; } = null;
        public NodeClass? NodeClass { get; set; } = null;

        public Dictionary<NodeId, HierarchicalNode> hierarchicalChilds = new Dictionary<NodeId, HierarchicalNode>();
        public HierarchicalNode(NodeId nodeId, ExpandedNodeId typeId, QualifiedName browseName)
        {
            NodeId = nodeId;
            TypeId = typeId;
            BrowseName = browseName;
        }
    }
}
