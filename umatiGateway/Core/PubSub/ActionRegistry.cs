// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.

namespace umatiGateway.Core.PubSub
{
    public sealed class ActionRegistry
    {
        private readonly Dictionary<ushort, ActionTargetMetadata> targets = new();

        public void Rebuild(IEnumerable<ActionMetadata> metadata)
        {
            targets.Clear();
            List<ActionTargetMetadata> orderedTargets = metadata
                .SelectMany(item => item.ActionTargets)
                .OrderBy(item => item.ObjectId, StringComparer.Ordinal)
                .ThenBy(item => item.MethodId, StringComparer.Ordinal)
                .ToList();

            if (orderedTargets.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException("More action targets were discovered than can be represented by UInt16.");
            }

            for (int index = 0; index < orderedTargets.Count; index++)
            {
                ActionTargetMetadata target = orderedTargets[index];
                target.ActionTargetId = checked((ushort)(index + 1));
                targets.Add(target.ActionTargetId, target);
            }
        }

        public bool TryGet(ushort actionTargetId, out ActionTargetMetadata? target)
        {
            return targets.TryGetValue(actionTargetId, out target);
        }
    }
}
