// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
namespace UmatiGateway.OPC
{
    public class Tree
    {
        public Dictionary<string, TreeNode> uids { get; set; }
        public String? SelectedTreeNode { get; set; }
        public LinkedList<TreeNode> children { get; set; }
        public bool Initialized { get; set; } = false;
        public Tree()
        {
            this.SelectedTreeNode = null;
            this.uids = new Dictionary<string, TreeNode>();
            this.children = new LinkedList<TreeNode>();
        }
    }
}
