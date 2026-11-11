// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

namespace umatiGateway.Core.PubSub
{
    public interface IActionExecutor
    {
        Task<ActionExecutionResult> ExecuteAsync(ActionRequest request);
    }
}
