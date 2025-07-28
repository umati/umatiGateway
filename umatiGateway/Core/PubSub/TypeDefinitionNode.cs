// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.PubSub
{
    public class TypeDefinitionNode
    {
        ExpandedNodeId NodeId { get; set; }
        IList<TypeChild> Children { get; set; } = new List<TypeChild>();
        public TypeDefinitionNode(NodeId nodeId)
        {
            NodeId = nodeId;
        }
    }
}
