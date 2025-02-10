#!/bin/bash
# SPDX-License-Identifier: Apache-2.0
# Copyright (c) 2025 Sebastian Friedl FVA GmbH - interop4x. All rights reserved.

export IMAGE_REPOSITORY=umati/umatigateway
export IMAGE_TAG=develop
docker compose up -d
pip3 install -r requirements.txt
./waitForContainer.sh
python3 test_mqtt_sampleserver.py
docker compose down
