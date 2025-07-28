// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.PubSub
{
    public class VirtualId
    {
        public NodeId nodeId;
        public DataValue dv;
        public VirtualId(NodeId nodeId, DataValue dv)
        {
            this.nodeId = nodeId;
            this.dv = dv;
        }
    }
}
