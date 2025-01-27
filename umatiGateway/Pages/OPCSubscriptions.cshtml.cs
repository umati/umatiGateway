// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Opc.Ua;
using System.Reflection;
using System.Resources;
using UmatiGateway.OPC;

namespace UmatiGateway.Pages
{
    public class OPCSubscriptionsModel : PageModel
    {
        public ClientFactory ClientFactory;
        public Tree? BrowseTree { get; private set; }
        public OPCSubscriptionsModel(ClientFactory ClientFactory)
        {
            this.ClientFactory = ClientFactory;
        }
        public void OnGet()
        {
            UmatiGatewayApp? client = this.getClient();
            if (client != null)
            {
                client.BrowseRootNode();
                this.BrowseTree = client.BrowseTree;
            }
            else
            {
                //Put an Error here that the Client is not connected
            }

        }
        public IActionResult OnPostPublishNode(string uuid)
        {
            UmatiGatewayApp? client = this.getClient();
            if (client != null)
            {
                this.BrowseTree = client.BrowseTree;
                this.BrowseTree.SelectedTreeNode = uuid;
                TreeNode? selectedTreeNode = this.GetForUid(uuid);
                if (selectedTreeNode != null)
                {
                    client.publishNode(selectedTreeNode.NodeData.node.NodeId);
                }
            }
            return new PageResult();
        }
        public IActionResult OnPostBrowseSelectedTreeNode(string uuid)
        {
            UmatiGatewayApp? client = this.getClient();
            if (client != null)
            {
                this.BrowseTree = client.BrowseTree;
                this.BrowseTree.SelectedTreeNode = uuid;
                TreeNode? selectedTreeNode = this.GetForUid(uuid);
                if (selectedTreeNode != null && selectedTreeNode.IsExpanded == false)
                {
                    selectedTreeNode.IsExpanded = true;
                    client.BrowseSelectedTreeNode(selectedTreeNode);
                }
                else if (selectedTreeNode != null && selectedTreeNode.IsExpanded == true)
                {
                    selectedTreeNode.IsExpanded = false;
                    selectedTreeNode.children.Clear();
                }

            }
            else
            {
                //Put an Error here that the Client is not connected
            }
            return new PageResult();
        }
        private UmatiGatewayApp? getClient()
        {
            UmatiGatewayApp? client = null;
            string? mySessionId = HttpContext.Session.GetString("SessionId");
            if (mySessionId != null)
            {
                client = this.ClientFactory.getClient(mySessionId);
            }
            return client;
        }
        public TreeNode? GetForUid(string? uid)
        {
            if (uid == null) { return null; }
            UmatiGatewayApp? client = this.getClient();
            if (client != null)
            {
                this.BrowseTree = client.BrowseTree;
                if (this.BrowseTree.uids.TryGetValue(uid, out var node))
                {
                    //client.BrowseSelectedTreeNode(node);
                    return node;
                }
            }
            return null;
        }
    }
}
