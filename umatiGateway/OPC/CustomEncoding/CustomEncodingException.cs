// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace UmatiGateway.OPC.CustomEncoding
{
    /// <summary>
    /// This Exception is used if any error happened during Custom Decoding of an DataType.
    /// </summary>
    public class CustomEncodingException : Exception
    {
        public CustomEncodingException()
        {
        }

        public CustomEncodingException(string message)
            : base(message)
        {
        }

        public CustomEncodingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}