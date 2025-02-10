export  IMAGE_REPOSITORY=umati/umatigateway
export IMAGE_TAG=develop
docker compose up -d
python3 -m venv path/to/venv
source path/to/venv/bin/activate
pip3 install -r requirements.txt
./waitForContainer.sh
python3 test_mqtt_sampleserver.py