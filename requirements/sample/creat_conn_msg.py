"""This module This module handles the creation of connection messages."""
import os
import json

# Konfiguration
BaseDir = "umati/v3/"
PublisherId = "example_publisher_1"
WriterGroupName = "MachineDataWriterGroup"

# Suchpfad zu den DataSet Topics
data_base_path = os.path.join(BaseDir, "data", PublisherId)
metadata_base_path = os.path.join(BaseDir, "metadata", PublisherId)

# Alle Topics (relativ zum PublisherId), in denen eine passende JSON-Datei liegt
dataset_writers = []

for root, dirs, files in os.walk(data_base_path):
    # Ermittle den aktuellen Ordnernamen
    current_folder = os.path.basename(root)

    # Erwarte eine Datei im Format <Ordnername>.json
    expected_json_filename = f"{current_folder}.json"

    if expected_json_filename in files:
        # Berechne Topic relativ zum data_base_path
        relative_topic = os.path.relpath(root, data_base_path).replace("\\", "/")

        # Erzeuge DataSetWriter-Eintrag
        writer = {
            "Name": relative_topic.replace("/", "_"),
            "QueueName": f"umati/v3/json/data/{PublisherId}/{relative_topic}",
            "MetaDataQueueName": f"umati/v3/json/metadata/{PublisherId}/{relative_topic}"
        }
        dataset_writers.append(writer)

# Finales JSON-Objekt
connection_payload = {
    "PublisherId": PublisherId,
    "WriterGroups": [
        {
            "Name": WriterGroupName,
            "DataSetWriters": dataset_writers
        }
    ]
}

# Speichern in Datei
output_path = os.path.join("opcua", "json", "connection", PublisherId)
os.makedirs(output_path, exist_ok=True)

with open(os.path.join(output_path, "connection.json"), "w") as f:
    json.dump(connection_payload, f, indent=2)

print(f"connection.json erfolgreich erstellt unter:\n{output_path}")
