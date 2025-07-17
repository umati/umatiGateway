// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

namespace umatiGateway.Core.OPC
{
    /// <summary>
    /// This class represents the state of the OpcUa Client.
    /// </summary>
    public class OpcUaClientState
    {
        private int blocked = 0;
        public OpcUaConnectionState ConnectionState { get; set; } = OpcUaConnectionState.Idle;
        public string Detail { get; set; } = "";
        public OpcUaClientState(OpcUaConnectionState connectionState, string detail = "")
        {
            this.ConnectionState = connectionState;
            this.Detail = detail;
        }
        public OpcUaClientState Copy()
        {
            OpcUaClientState copy = new OpcUaClientState(this.ConnectionState, this.Detail);
            copy.blocked = this.blocked;
            return copy;
        }
        public void setState(OpcUaConnectionState connectionState, string detail = "")
        {
            this.ConnectionState = connectionState;
            this.Detail = detail;
        }
        public bool TrySetBlocked()
        {
            return Interlocked.CompareExchange(ref blocked, 1, 0) == 0;
        }

        public void ClearBlocked()
        {
            Interlocked.Exchange(ref blocked, 0);
        }

        public bool IsBlocked => blocked != 0;
    }
    public enum OpcUaConnectionState {
        Idle,
        Connecting,
        Connected,
        Disconnecting,
        Error
    }
}
