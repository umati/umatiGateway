// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace umatiGateway.Core.OPC
{
    /// <summary>
    /// Utility class for creating BrowseDescriptions and Filters for better usage of the Browse service.
    /// </summary>
    public static class BrowseUtils
    {
        /// <summary>
        /// Returns a BrowseDescription that gets the TypeDefinition NodeId for a NodeId.
        /// </summary>
        /// <param name="nodeId">The NodeId for that the Typedefinition should be retrieved.</param>
        /// <returns>The BrowseDescription that gets the TypeDefinition NodeId for a NodeId.</returns>
        public static BrowseDescription GetTypeDefinition(NodeId nodeId)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = (uint)NodeClass.Unspecified;
            browseDescription.ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition;
            browseDescription.IncludeSubtypes = true;
            return browseDescription;
        }
        /// <summary>
        /// Returns a BrowseDescription that gets the ModellingRuleDescription NodeId for a NodeId.
        /// </summary>
        /// <param name="nodeId">The NodeId for that the ModellingRule NodeIds should be retrieved.</param>
        /// <returns>The BrowseDescription that gets the ModellingRule NodeId for a NodeId.</returns>
        public static BrowseDescription GetModellingRule(NodeId nodeId)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = (uint)NodeClass.Unspecified;
            browseDescription.ReferenceTypeId = ReferenceTypeIds.HasModellingRule;
            browseDescription.IncludeSubtypes = false;
            return browseDescription;
        }
        /// <summary>
        /// Returns a BrowseDescription that browses all hierarchical Children for a node.
        /// </summary>
        /// <param name = "nodeId">The NodeId for that the children are browsed.</param>
        /// <param name = "nodeClassMask">The Nodeclasses of the Childs</param>
        /// <returns>The BrowseDescription that gets the hierarchical Children for one Node.</returns>
        public static BrowseDescription GetHierarchicalChildren(NodeId nodeId, uint nodeCassMask = (uint) NodeClass.Unspecified)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = (uint)NodeClass.Unspecified;
            browseDescription.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
            browseDescription.IncludeSubtypes = true;
            return browseDescription;
        }
        /// <summary>
        /// Returns a BrowseDescription that browses all hierarchical Children for a node.
        /// </summary>
        /// <param name = "nodeId">The NodeId for that the children are browsed.</param>
        /// <param name = "nodeClassMask">The Nodeclasses of the Childs</param>
        /// <returns>The BrowseDescription that gets the hierarchical Children for one Node.</returns>
        public static BrowseDescription ForwardBrowseDescription(NodeId nodeId, uint nodeCassMask, NodeId referenceTypeId, bool includeSubTypes)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = nodeCassMask;
            browseDescription.ReferenceTypeId = referenceTypeId;
            browseDescription.IncludeSubtypes = includeSubTypes;
            return browseDescription;
        }
        public static BrowseDescription InverseBrowseDescription(NodeId nodeId, uint nodeCassMask, NodeId referenceTypeId, bool includeSubTypes)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Inverse;
            browseDescription.NodeClassMask = nodeCassMask;
            browseDescription.ReferenceTypeId = referenceTypeId;
            browseDescription.IncludeSubtypes = includeSubTypes;
            return browseDescription;
        }

    }
}
