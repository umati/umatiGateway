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
            Dictionary<NodeId, MachineNode> filteredMachineNodes = new Dictionary<NodeId, MachineNode>();
            foreach (PublishedNode publishedConfigNode in publishedNodes)
            {
                switch (publishedConfigNode)
                {
                    case PublishedChildNodes publishedChildNodes:
                        NodeId? resolvedNodeId = this.ResolveNodeId(publishedChildNodes.Type, publishedChildNodes.NodeId, publishedChildNodes.NamespaceUrl, publishedChildNodes.BaseType);
                        if (resolvedNodeId != null)
                        {
                            List<NodeId> childNodes = client.BrowseNodeIds(new BrowseDescriptionCollection { BrowseUtils.GetHierarchicalChildren(resolvedNodeId, (int)NodeClass.Object | (int)NodeClass.Variable) });
                            //List<NodeId> childNodes = client.BrowseLocalNodeIds(resolvedNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true);
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
                                    childMachineNode.NamespaceUrl = this.client.GetNamespaceTable().GetString(child.NamespaceIndex);
                                    childMachineNode.NodeIdString = child.Identifier.ToString() ?? "";
                                    filteredMachineNodes.Add(child, childMachineNode);
                                }
                                else
                                {
                                    Logger.Info($"NodeId {child} allready added to published Maschines.");
                                }
                            }
                        }
                        else
                        {
                            Logger.Error($"PublishedChildNodes could not be resolved to a NodeId{publishedChildNodes}");
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
                                Logger.Info($"NodeId {nodeId} allready added to published Maschines.");
                            }
                        }
                        else
                        {
                            Logger.Error($"PublishedNode could not be resolved to a NodeId{publishedNode}");
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
                return this.client.BrowseLocalNodeIds(parentNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, relationTypeId, relationCondition.IncludeSubTypes);
            }
            else
            {
                Logger.Error($"Unresolveable NodeId for RelationCondition: {relationCondition}");
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
                    Logger.Error($"Unimplemented FilterType: {filter.FilterType.GetType()}");
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
                        Logger.Error($"Unimplemented ConditionType: {condition.GetType()}");
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
                    Logger.Error($"Unimplemented ConditionType: {conditions.ConditionType}");
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
                return this.client.BrowseLocalNodeIdsWithTypeDefinition(parentNodeId, BrowseDirection.Forward, (int)NodeClass.Object | (int)NodeClass.Variable, ReferenceTypeIds.HierarchicalReferences, true, typeId);
            }
            else
            {
                Logger.Error($"Unresolveable NodeId for TypeIdCondition: {typeIdCondition}");
                return new List<NodeId>();
            }
        }
        private NodeId? ResolveNodeId(string nodeIdType, string nodeId, string namespaceurl, string baseType)
        {
            NodeId? resolvedNodeId = null;
            Logger.Debug($"Read Machine Node: {nodeIdType}\t{nodeId}\t{namespaceurl}\t{baseType}");
            int namespaceIndex = client.GetNamespaceTable().GetIndex(namespaceurl);
            if (nodeIdType == "Numeric")
            {
                resolvedNodeId = new NodeId(Convert.ToUInt32(nodeId), (ushort)namespaceIndex);
                Logger.Debug($"Resolved NodeId is:\t{resolvedNodeId}");
            }
            else if (nodeIdType == "String")
            {
                resolvedNodeId = new NodeId(nodeId, (ushort)namespaceIndex);
                Logger.Debug($"Resolved NodeId is:\t{resolvedNodeId}");
            }
            else
            {
                Logger.Error($"Unknown NodeIdType {nodeIdType}");
            }
            return resolvedNodeId;
        }

    }
}
