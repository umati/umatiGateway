// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace umatiGateway.Core.OPC
{
    public interface UmatiGatewayAppListener
    {
        public void blockingTransitionChanged(BlockingTransition blockingTransition);
    }
}
