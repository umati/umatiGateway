// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Opc.Ua;

namespace UmatiGateway.OPC
{
    /// <summary>
    /// Utility class for creating BrowseDescriptions and Filters for better usage of the Browse service.
    /// </summary>
    public static class BrowseUtils
    {
        /// <summary>
        /// Returns a BrowseDescription that gets the TypeDefinition for a Node.
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static BrowseDescription TypeDefinitionBrowseDescription(NodeId nodeId)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = (uint)NodeClass.Unspecified;
            browseDescription.ReferenceTypeId = ReferenceTypeIds.HasTypeDefinition;
            browseDescription.IncludeSubtypes = true;
            return browseDescription;
        }
        public static BrowseDescription ModellingRuleBrowseDescription(NodeId nodeId)
        {
            BrowseDescription browseDescription = new BrowseDescription();
            browseDescription.NodeId = nodeId;
            browseDescription.BrowseDirection = BrowseDirection.Forward;
            browseDescription.NodeClassMask = (uint)NodeClass.Unspecified;
            browseDescription.ReferenceTypeId = ReferenceTypeIds.HasModellingRule;
            browseDescription.IncludeSubtypes = false;
            return browseDescription;
        }
        public static void FilterContext(NodeId nodeId)
        {
        }
    }
}
