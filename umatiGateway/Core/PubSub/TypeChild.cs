// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.PubSub
{
    public class TypeChild
    {
        NodeId ReferenceTypeId { get; set; }
        NodeId ModellingRule { get; set; }
        NodeId NodeId { get; set; }
        NodeId Origin { get; set; }
        NodeClass NodeClass { get; set; }

        public TypeChild(NodeId nodeId, NodeClass nodeClass, NodeId referenceTypeId, NodeId origin, NodeId modellingRule)
        {
            NodeId = nodeId;
            ReferenceTypeId = referenceTypeId;
            Origin = origin;
            NodeClass = nodeClass;
            ModellingRule = modellingRule;
        }
    }
}
