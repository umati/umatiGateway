import os
import json

# Konfiguration
base_dir = "umati/v3/"
publisher_id = "example_publisher_1"
writer_group_name = "MachineDataWriterGroup"

# Suchpfad zu den DataSet Topics
data_base_path = os.path.join(base_dir, "data", publisher_id)
metadata_base_path = os.path.join(base_dir, "metadata", publisher_id)

# Alle Topics (relativ zum PublisherId), in denen payload.json liegt
dataset_writers = []

for root, dirs, files in os.walk(data_base_path):
    print(root, dirs,files)
    if "payload.json" in files:
        # Berechne Topic relativ zum data_base_path
        relative_topic = os.path.relpath(root, data_base_path).replace("\\", "/")

        # Erzeuge DataSetWriter-Eintrag
        writer = {
            "Name": relative_topic.replace("/", "_"),
            "QueueName": f"umati/v3/json/data/{publisher_id}/{relative_topic}",
            "MetaDataQueueName": f"umati/v3/json/metadata/{publisher_id}/{relative_topic}"
        }
        dataset_writers.append(writer)

# Finales JSON-Objekt
connection_payload = {
    "PublisherId": publisher_id,
    "WriterGroups": [
        {
            "Name": writer_group_name,
            "DataSetWriters": dataset_writers
        }
    ]
}

# Speichern in Datei
output_path = os.path.join("opcua", "json", "connection", publisher_id)
os.makedirs(output_path, exist_ok=True)

with open(os.path.join(output_path, "payload.json"), "w") as f:
    json.dump(connection_payload, f, indent=2)

print(f"connection payload.json erfolgreich erstellt unter:\n{output_path}")
