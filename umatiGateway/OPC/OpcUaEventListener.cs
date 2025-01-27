// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace UmatiGateway.OPC
{
    public interface OpcUaEventListener
    {
        public void ModelChangeEvent(NodeId affectedNode);
    }
}
