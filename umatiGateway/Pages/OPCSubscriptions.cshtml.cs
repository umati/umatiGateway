// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Opc.Ua;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    public class OPCSubscriptionsModel : PageModel
    {
        public Tree? BrowseTree { get; private set; }
        public UmatiGatewayApp app { get; private set; }
        public OPCSubscriptionsModel(ClientFactory ClientFactory)
        {
            this.app = ClientFactory.getClient();
        }
        public void OnGet()
        {
            app.BrowseTreeController.BrowseRootNode();
            this.BrowseTree = app.BrowseTreeController.BrowseTree;
        }
        public IActionResult OnPostAddNodeMqttConfig(string uuid)
        {
            this.BrowseTree = app.BrowseTreeController.BrowseTree;
            this.BrowseTree.SelectedTreeNode = uuid;
            TreeNode? selectedTreeNode = this.GetForUid(uuid);
            if (selectedTreeNode != null)
            {
                NodeId nodeId = selectedTreeNode.NodeData.node.NodeId;
                string? namespaceUrl = this.app.OpcUaClient.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
                string? identifier = nodeId.Identifier.ToString();
                if (namespaceUrl != null && identifier != null)
                {
                    PublishedNode publishedNode = new PublishedNode();
                    publishedNode.NamespaceUrl = namespaceUrl;
                    publishedNode.Type = nodeId.IdType.ToString();
                    publishedNode.NodeId = identifier;
                    publishedNode.BaseType = "";
                    this.app.ActiveConfiguration.MqttProviderConfig.PublishedNodes.Add(publishedNode);
                }
            }
            return new PageResult();
        }
        public IActionResult OnPostAddNodeOpcPubSubConfig(string uuid)
        {
            this.BrowseTree = app.BrowseTreeController.BrowseTree;
            this.BrowseTree.SelectedTreeNode = uuid;
            TreeNode? selectedTreeNode = this.GetForUid(uuid);
            if (selectedTreeNode != null)
            {
                NodeId nodeId = selectedTreeNode.NodeData.node.NodeId;
                string? namespaceUrl = this.app.OpcUaClient.GetNamespaceTable().GetString(nodeId.NamespaceIndex);
                string? identifier = nodeId.Identifier.ToString();
                if (namespaceUrl != null && identifier != null)
                {
                    PublishedNode publishedNode = new PublishedNode();
                    publishedNode.NamespaceUrl = namespaceUrl;
                    publishedNode.Type = nodeId.IdType.ToString();
                    publishedNode.NodeId = identifier;
                    publishedNode.BaseType = "";
                    this.app.ActiveConfiguration.PubSubProviderConfig.PublishedNodes.Add(publishedNode);
                }
            }
            return new PageResult();
        }
        public IActionResult OnPostBrowseSelectedTreeNode(string uuid)
        {
            this.BrowseTree = app.BrowseTreeController.BrowseTree;
            this.BrowseTree.SelectedTreeNode = uuid;
            TreeNode? selectedTreeNode = this.GetForUid(uuid);
            if (selectedTreeNode != null && selectedTreeNode.IsExpanded == false)
            {
                selectedTreeNode.IsExpanded = true;
                app.BrowseTreeController.BrowseSelectedTreeNode(selectedTreeNode);
            }
            else if (selectedTreeNode != null && selectedTreeNode.IsExpanded == true)
            {
                selectedTreeNode.IsExpanded = false;
                selectedTreeNode.children.Clear();
            }
            return new PageResult();
        }
        public TreeNode? GetForUid(string? uid)
        {
            if (uid == null) { return null; }
            this.BrowseTree = app.BrowseTreeController.BrowseTree;
            if (this.BrowseTree.uids.TryGetValue(uid, out var node))
            {
                //client.BrowseSelectedTreeNode(node);
                return node;
            }
            return null;
        }
    }
}
