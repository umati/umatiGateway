"""This module This module handles the creation of connection messages."""

import json
import os


# Konfiguration
BASE_DIR = "umati/v3/"
PUBLISHER_ID = "example_publisher_1"
WRITER_GROUP_NAME = "MachineDataWriterGroup"

# Suchpfad zu den DataSet Topics
data_base_path = os.path.join(BASE_DIR, "data", PUBLISHER_ID)
metadata_base_path = os.path.join(BASE_DIR, "metadata", PUBLISHER_ID)

# Alle Topics (relativ zum PUBLISHER_ID), in denen eine passende JSON-Datei liegt
dataset_writers = []

for root, dirs, files in os.walk(data_base_path):
    # Ermittle den aktuellen Ordnernamen
    current_folder = os.path.basename(root)

    # Erwarte eine Datei im Format <Ordnername>.json
    EXPECTED_JSON_FILENAME = f"{current_folder}.json"

    if EXPECTED_JSON_FILENAME in files:
        # Berechne Topic relativ zum data_base_path
        relative_topic = os.path.relpath(root, data_base_path).replace("\\", "/")

        # Erzeuge DataSetWriter-Eintrag
        writer = {
            "Name": relative_topic.replace("/", "_"),
            "QueueName": f"umati/v3/json/data/{PUBLISHER_ID}/{relative_topic}",
            "MetaDataQueueName": f"umati/v3/json/metadata/{PUBLISHER_ID}/{relative_topic}",
        }
        dataset_writers.append(writer)

# Finales JSON-Objekt
connection_payload = {
    "PUBLISHER_ID": PUBLISHER_ID,
    "WriterGroups": [{"Name": WRITER_GROUP_NAME, "DataSetWriters": dataset_writers}],
}

# Speichern in Datei
output_path = os.path.join("opcua", "json", "connection", PUBLISHER_ID)
os.makedirs(output_path, exist_ok=True)

with open(os.path.join(output_path, "connection.json"), "w", encoding="utf-8") as f:
    json.dump(connection_payload, f, indent=2)

print(f"connection.json erfolgreich erstellt unter:\n{output_path}")
