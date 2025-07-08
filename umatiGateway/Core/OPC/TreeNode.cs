// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace umatiGateway.Core.OPC
{
    public class TreeNode
    {
        public string uid { get; set; }
        public NodeData NodeData { get; set; }
        public LinkedList<TreeNode> children { get; set; }
        public bool IsExpanded { get; set; }

        public TreeNode(NodeData NodeData)
        {
            uid = Guid.NewGuid().ToString();
            this.NodeData = NodeData;
            children = new LinkedList<TreeNode>();
            IsExpanded = false;
        }
    }
}
