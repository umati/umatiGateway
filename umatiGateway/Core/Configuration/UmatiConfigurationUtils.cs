// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace umatiGateway.Core.Configuration
{
    /// <summary>
    /// Static helper class for  UmatiConfiguration.
    /// </summary>
    public static class UmatiConfigurationUtils
    {
        /// <summary>
        /// Returns a Password mask with the same number of digits than the password.
        /// </summary>
        public static string MaskPassword(string password)
        {
            return string.IsNullOrEmpty(password) ? "" : new string('*', password.Length);
        }
    }
}
