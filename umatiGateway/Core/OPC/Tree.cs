// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace umatiGateway.Core.OPC
{
    public class Tree
    {
        public Dictionary<string, TreeNode> uids { get; set; }
        public string? SelectedTreeNode { get; set; }
        public LinkedList<TreeNode> children { get; set; }
        public bool Initialized { get; set; } = false;
        public Tree()
        {
            SelectedTreeNode = null;
            uids = new Dictionary<string, TreeNode>();
            children = new LinkedList<TreeNode>();
        }
    }
}
