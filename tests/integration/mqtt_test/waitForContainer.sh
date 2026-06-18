#!/bin/bash
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2025 Sebastian Friedl FVA GmbH - interop4x. All rights reserved.
# Copyright (c) 2026 Goetz Goerisch - VDW e.V.. All rights reserved.

NEXT_WAITTIME=0
WAITTIME_LIMIT_SEC=1200

while [[ $(docker logs mqtt_test-gateway-1 2>&1 | grep -c "INFO Publish Online Machines Machine Node finish") -le 4 && $NEXT_WAITTIME -lt $WAITTIME_LIMIT_SEC ]]; do
    count=$(docker logs mqtt_test-gateway-1 2>&1 | grep -c "INFO Publish Online Machines Machine Node finish")

    echo "Waiting for test container to become ready... (${NEXT_WAITTIME}s elapsed, found $count entries)"
    echo $(docker logs mqtt_test-gateway-1 2>&1)
    sleep 5
    NEXT_WAITTIME=$((NEXT_WAITTIME + 5))
done

final_count=$(docker logs mqtt_test-gateway-1 2>&1 | grep -c "INFO Publish Online Machines Machine Node finish")

if [[ $final_count -gt 4 ]]; then
    echo "✅ Container is ready (found $final_count log entries)."
else
    echo "❌ Timeout reached after ${NEXT_WAITTIME}s. Container not ready (found $final_count entries)."
    exit 1
fi