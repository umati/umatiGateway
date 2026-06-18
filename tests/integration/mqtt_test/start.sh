#!/bin/bash
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2025 Sebastian Friedl FVA GmbH - interop4x. All rights reserved.

export IMAGE_REPOSITORY=umati/umatigateway
export IMAGE_TAG=develop
echo $IMAGE_REPOSITORY
echo $IMAGE_TAG
export CONFIG_FILE="$(pwd)/SimpleConfig/umatiGatewayConfig.xml"
docker compose up -d
python3 -m venv .venv
source .venv/bin/activate
python3 -m pip install --upgrade pip
pip install -r requirements.txt
./waitForContainer.sh
python3 test_mqtt_sampleserver.py
docker compose down
