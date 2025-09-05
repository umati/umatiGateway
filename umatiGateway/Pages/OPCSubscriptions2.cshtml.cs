// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Opc.Ua;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace UmatiGateway.Pages
{
    [IgnoreAntiforgeryToken]
    public class OPCSubscriptions2Model : PageModel
    {
        public UmatiGatewayApp app { get; private set; }
        public OPCSubscriptions2Model(ClientFactory ClientFactory)
        {
            this.app = ClientFactory.getClient();
        }
        public void OnGet()
        {
        }

        public JsonResult OnPostAddNodeMqttConfig([FromBody] OpcUaNode opcUaNode)
        {

            NodeId nodeId = new NodeId(opcUaNode.NodeId);
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
            return new JsonResult(new { success = false });
        }
        public JsonResult OnPostAddNodePubSubConfig([FromBody] OpcUaNode opcUaNode)
        {
            NodeId nodeId = new NodeId(opcUaNode.NodeId);
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
            return new JsonResult(new { success = false });
        }

        public JsonResult OnPostBrowseTreeNode([FromBody] OpcUaNode opcUaNode)
        {
            try
            {
                try
                {
                    this.app.OpcUaClient.CheckSession();
                    NodeId nodeId = new NodeId(opcUaNode.NodeId);
                    BrowseResultCollection browseResultCollection = this.app.OpcUaClient.BrowseNode(nodeId, BrowseDirection.Forward, (uint)0xFF, ReferenceTypeIds.HierarchicalReferences, true);
                    List<OpcUaNode> childNodes = new List<OpcUaNode>();
                    foreach (BrowseResult browseResult in browseResultCollection)
                    {

                        ReferenceDescriptionCollection referenceDescriptionCollection = browseResult.References;
                        foreach (ReferenceDescription referenceDescription in referenceDescriptionCollection)
                        {
                            OpcUaNode childNode = new OpcUaNode();
                            childNode.DisplayName = referenceDescription.DisplayName.Text;
                            childNode.BrowseName = referenceDescription.BrowseName.ToString();
                            childNode.NodeId = referenceDescription.NodeId.ToString();
                            childNode.TypeId = referenceDescription.TypeId.ToString();
                            childNode.NodeClass = referenceDescription.NodeClass.ToString();

                            NodeId childNodeId = new NodeId(referenceDescription.NodeId.Identifier, referenceDescription.NodeId.NamespaceIndex);
                            Node? node = this.app.OpcUaClient.ReadNode(childNodeId);
                            if (node != null)
                            {
                                if (node.Description != null)
                                {
                                    childNode.Description = node.Description.ToString();
                                }
                                else
                                {
                                    childNode.Description = "";
                                }
                            }
                            try
                            {
                                DataValue? value = this.app.OpcUaClient.ReadValue(childNodeId);
                                if (value != null)
                                {
                                    childNode.Value = value.ToString();
                                }
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine(exception);
                            }

                            childNodes.Add(childNode);
                        }
                    }
                    return new JsonResult(childNodes.ToArray());
                }
                catch (OpcUaException opcUaException)
                {
                    Console.WriteLine(opcUaException.ToString());
                    return new JsonResult(new { success = false, message = $"Fehler: {opcUaException.Message}" });
                }

            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Fehler: {ex.Message}" });
            }
        }
    }
    public class OpcUaNode
    {
        public string DisplayName { get; set; } = "";
        public string BrowseName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Value { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string TypeId { get; set; } = "";
        public string NodeClass { get; set; } = "";
        public OpcUaNode[] HierarchicalChildren { get; set; } = new OpcUaNode[0];
    }
}
