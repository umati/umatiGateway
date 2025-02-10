#!/bin/bash
export  IMAGE_REPOSITORY=umati/umatigateway
export IMAGE_TAG=develop
docker compose up -d
pip3 install -r requirements.txt
./waitForContainer.sh
python3 test_mqtt_sampleserver.py
docker compose down