using NLog;
using Opc.Ua;
using Org.BouncyCastle.Asn1.Ocsp;
using umatiGateway.Core.Configuration;
using umatiGateway.Core.OPC;

namespace umatiGateway.Core.Mqtt
{
    public class PublishedNodeFilter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UmatiGatewayApp app;
        private IOpcUaClient client;
        public PublishedNodeFilter(UmatiGatewayApp app)
        {
            this.app = app;
            this.client = app.OpcUaClient;
        }
        public List<MachineNode> FilterMachineNodes(List<PublishedNode> publishedNodes)
        {
            NamespaceTable namespaceTable = new NamespaceTable();
            if(client.TryGetNamespaceTable(out NamespaceTable nameSpaceTable1))
            {
                namespaceTable = nameSpaceTable1;
            }
            Dictionary<NodeId, MachineNode> filteredMachineNodes = new Dictionary<NodeId, MachineNode>();
            foreach (PublishedNode publishedConfigNode in publishedNodes)
            {
                switch (publishedConfigNode)
                {
                    case PublishedChildNodes publishedChildNodes:
                        NodeId? resolvedNodeId = this.ResolveNodeId(publishedChildNodes.Type, publishedChildNodes.NodeId, publishedChildNodes.NamespaceUrl, publishedChildNodes.BaseType);
                        if (resolvedNodeId != null)
                        {
                            if (client.TryBrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(resolvedNodeId, (int)NodeClass.Object | (int)NodeClass.Variable) }, out List<NodeId> childNodes))
                            {
                                List<NodeId> filteredNodes = this.MatchFilters(resolvedNodeId, childNodes, publishedChildNodes.Filter);
                                foreach (NodeId child in filteredNodes)
                                {
                                    if (!filteredMachineNodes.ContainsKey(child))
                                    {
                                        MachineNode childMachineNode = new MachineNode(publishedChildNodes.NodeId, publishedChildNodes.NamespaceUrl);
                                        childMachineNode.NodeIdType = publishedChildNodes.Type;
                                        childMachineNode.BaseType = publishedChildNodes.BaseType;
                                        childMachineNode.PublishedNodeType = "PublishedChildNodes";
                                        childMachineNode.ResolvedNodeId = child;
                                        childMachineNode.NamespaceUrl = namespaceTable.GetString(child.NamespaceIndex);
                                        childMachineNode.NodeIdString = child.Identifier.ToString() ?? "";
                                        filteredMachineNodes.Add(child, childMachineNode);
                                    }
                                    else
                                    {
                                        Logger.Info("NodeId {Child} already added to published Machines.", child);
                                    }
                                }
                            }
                            else
                            {
                                Logger.Error("Unable to browse NodeIds");
                            }
                        }
                        else
                        {
                            Logger.Error("PublishedChildNodes could not be resolved to a NodeId{PublishedChildNodes}", publishedChildNodes);
                        }
                        break;
                    case PublishedNode publishedNode:
                        NodeId? nodeId = this.ResolveNodeId(publishedNode.Type, publishedNode.NodeId, publishedNode.NamespaceUrl, publishedNode.BaseType);
                        if (nodeId != null)
                        {
                            if (!filteredMachineNodes.ContainsKey(nodeId))
                            {
                                MachineNode machineNode = new MachineNode(publishedNode.NodeId, publishedNode.NamespaceUrl);
                                machineNode.NodeIdType = publishedNode.Type;
                                machineNode.BaseType = publishedNode.BaseType;
                                machineNode.ResolvedNodeId = nodeId;
                                filteredMachineNodes.Add(nodeId, machineNode);
                            }
                            else
                            {
                                Logger.Info("NodeId {NodeId} already added to published Machines.", nodeId);
                            }
                        }
                        else
                        {
                            Logger.Error("PublishedNode could not be resolved to a NodeId{PublishedNode}", publishedNode);
                        }
                        break;
                    default:
                        break;
                }
            }
            return filteredMachineNodes.Values.ToList<MachineNode>();
        }
        private List<NodeId> MatchFilterRelationCondition(NodeId parentNodeId, RelationCondition relationCondition)
        {
            NodeId? relationTypeId = this.ResolveNodeId(relationCondition.Type, relationCondition.NodeId, relationCondition.NamespaceUrl, "");
            if (relationTypeId != null)
            {
                if (this.client.TryBrowseLocalNodeIds(parentNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, relationTypeId, relationCondition.IncludeSubTypes, out List<NodeId> localNodeIds))
                {
                    return localNodeIds;
                }
                else
                {
                    HandleOpcUaClientError();
                    return new List<NodeId>();
                }
            }
            else
            {
                Logger.Error("Unresolveable NodeId for RelationCondition: {RelationCondition}", relationCondition);
                return new List<NodeId>();
            }
        }
        private List<NodeId> MatchFilters(NodeId parentNodeId, List<NodeId> childNodes, List<Filter> filters)
        {
            List<List<NodeId>> filteredIdsList = new List<List<NodeId>>();
            foreach (Filter filter in filters)
            {
                List<NodeId> conditionsFiltered = this.MatchFilterConditions(parentNodeId, filter.ConditionsList);
                if (filter.FilterType == FilterType.Whitelist)
                {
                    filteredIdsList.Add(childNodes.Intersect<NodeId>(conditionsFiltered).ToList());
                }
                else if (filter.FilterType == FilterType.Blacklist)
                {
                    filteredIdsList.Add(childNodes.Where(item => !conditionsFiltered.Contains(item)).ToList());
                }
                else
                {
                    Logger.Error("Not implemented FilterType: {FilterType}", filter.FilterType.GetType());
                }
            }
            List<NodeId> filteredIds = new List<NodeId>();
            foreach (List<NodeId> filterIdList in filteredIdsList)
            {
                filteredIds.AddRange(filterIdList);
            }
            filteredIds.Distinct().ToList();
            return filteredIds;
        }
        private List<NodeId> MatchFilterConditions(NodeId parentNodeId, List<Conditions> conditionsList)
        {
            List<NodeId> filterConditions = new List<NodeId>();
            foreach (Conditions conditions in conditionsList)
            {
                filterConditions.AddRange(this.MatchConditions(parentNodeId, conditions));
            }
            filterConditions.Distinct().ToList();
            return filterConditions;
        }
        private List<NodeId> MatchConditions(NodeId parentNodeId, Conditions conditions)
        {
            List<NodeId> nodeIdsMatchingConditions = new List<NodeId>();
            List<List<NodeId>> singleConditionNodeIdsList = new List<List<NodeId>>();
            foreach (Condition condition in conditions.ConditionList)
            {
                switch (condition)
                {
                    case RelationCondition relationCondition:
                        singleConditionNodeIdsList.Add(this.MatchFilterRelationCondition(parentNodeId, relationCondition));
                        break;
                    case TypeIdCondition typeIdCondition:
                        singleConditionNodeIdsList.Add(this.MatchFilterTypeIdCondition(parentNodeId, typeIdCondition));
                        break;
                    default:
                        Logger.Error("Not implemented ConditionType: {ConditionType}", condition.GetType());
                        break;
                }
            }
            bool firstList = true;
            foreach (List<NodeId> conditionList in singleConditionNodeIdsList)
            {

                if (conditions.ConditionType == ConditionType.And)
                {
                    if (firstList)
                    {
                        nodeIdsMatchingConditions.AddRange(conditionList);
                        firstList = false;
                    }
                    else
                    {
                        nodeIdsMatchingConditions = nodeIdsMatchingConditions.Intersect(conditionList).ToList();
                    }
                }
                else if (conditions.ConditionType == ConditionType.Or)
                {
                    nodeIdsMatchingConditions.AddRange(conditionList);
                }
                else
                {
                    Logger.Error("Not implemented ConditionType: {ConditionsType}", conditions.ConditionType);
                }
            }
            nodeIdsMatchingConditions.Distinct().ToList();
            return nodeIdsMatchingConditions;
        }
        private List<NodeId> MatchFilterTypeIdCondition(NodeId parentNodeId, TypeIdCondition typeIdCondition)
        {
            NodeId? typeId = this.ResolveNodeId(typeIdCondition.Type, typeIdCondition.NodeId, typeIdCondition.NamespaceUrl, "");
            if (typeId != null)
            {
                if(this.client.TryBrowseLocalNodeIdsWithTypeDefinition(parentNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, typeId, out List<NodeId> localNodeIds))
                {
                    return localNodeIds;
                }
                else
                {
                    HandleOpcUaClientError();
                    return new List<NodeId>();
                }
            }
            else
            {
                Logger.Error("Unresolveable NodeId for TypeIdCondition: {TypeIdCondition}", typeIdCondition);
                return new List<NodeId>();
            }
        }
        private NodeId? ResolveNodeId(string nodeIdType, string nodeId, string namespaceurl, string baseType)
        {
            NodeId? resolvedNodeId = null;
            Logger.Debug("Read Machine Node: {NodeIdType}\t{NodeId}\t{NamespaceUrl}\t{BaseType}", nodeIdType, nodeId, namespaceurl, baseType);
            NamespaceTable namespaceTable = new NamespaceTable();
            if(client.TryGetNamespaceTable(out NamespaceTable namespaceTable1))
            {
                namespaceTable = namespaceTable1;
            }
            int namespaceIndex = namespaceTable.GetIndex(namespaceurl);
            if (nodeIdType == "Numeric")
            {
                resolvedNodeId = new NodeId(Convert.ToUInt32(nodeId), (ushort)namespaceIndex);
                Logger.Debug("Resolved NodeId is:\t{ResolvedNodeId}", resolvedNodeId);
            }
            else if (nodeIdType == "String")
            {
                resolvedNodeId = new NodeId(nodeId, (ushort)namespaceIndex);
                Logger.Debug("Resolved NodeId is:\t{ResolvedNodeId}", resolvedNodeId);
            }
            else
            {
                Logger.Error("Unknown NodeIdType {nodeIdType}");
            }
            return resolvedNodeId;
        }
        private void HandleOpcUaClientError()
        {
            Logger.Error("Unable to retrieve Data form OpcUaClient");
        }
    }
}
