export  IMAGE_REPOSITORY=umati/umatigateway
export IMAGE_TAG=develop
docker compose up
#./waitForContainer.sh
#python3 test_mqtt_sampleserver.py