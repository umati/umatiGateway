#!/bin/bash
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2025 Sebastian Friedl FVA GmbH - interop4x. All rights reserved.

NEXT_WAITTIME=0
WAITTIME_LIMIT_SEC=600
echo "Waiting for test container to become ready since ${NEXT_WAITTIME}s..."
while [[ "$(docker logs mqtt_test-gateway-1 | grep -c "Publish Bad List Maschine Nodes finish.")" -le "4" && "$NEXT_WAITTIME" != "$WAITTIME_LIMIT_SEC" ]]; do
	echo "Waiting for test container to become ready since ${NEXT_WAITTIME}s..."
	sleep 5
	NEXT_WAITTIME=$((NEXT_WAITTIME + 5))
done
sleep 5
