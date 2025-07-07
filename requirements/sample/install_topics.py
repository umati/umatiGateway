""" Module that installs topics"""
import os

# Basis-Konfiguration
PUBLISHER_ID = "example_publisher_1"
UNS = "UNS"
MACHINE_NAME = "FullMachineTool"
BASE_PATH = "umati/v3/"
MACHINE_PATH = f"{UNS}/machines/{MACHINE_NAME}/"

# Liste aller relevanten Topics relativ zur Maschine
topics = [
    # 2_Identification
    f"{MACHINE_PATH}2_Identification",
    f"{MACHINE_PATH}2_Identification/5_SoftwareIdentification"

    # 5_Equipment
    f"{MACHINE_PATH}5_Equipment/5_Tools/13_Multi_1/13_SubTool_0/5_ToolLife/13_Rotations",
    f"{MACHINE_PATH}5_Equipment/5_Tools/13_Multi_1/13_SubTool_1/5_ToolLife/13_Rotations",
    f"{MACHINE_PATH}5_Equipment/5_Tools/13_Multi_1/13_SubTool_2/5_ToolLife/13_Rotations",
    f"{MACHINE_PATH}5_Equipment/5_Tools/13_Tool1/13_ToolLife/13_Rotations",

    # 5_Monitoring
    f"{MACHINE_PATH}5_Monitoring/13_Channel_1",
    f"{MACHINE_PATH}5_Monitoring/13_Channel_2",
    f"{MACHINE_PATH}5_Monitoring/13_Channel_3",
    f"{MACHINE_PATH}5_Monitoring/13_Channel_4",
    f"{MACHINE_PATH}5_Monitoring/13_Spindle_1",
    f"{MACHINE_PATH}5_Monitoring/13_EDM",
    f"{MACHINE_PATH}5_Monitoring/13_Laser",
    f"{MACHINE_PATH}5_Monitoring/5_Stacklight/13_Light_0",
    f"{MACHINE_PATH}5_Monitoring/5_Stacklight/13_Light_1",
    f"{MACHINE_PATH}5_Monitoring/5_Stacklight/13_Light_2",
    f"{MACHINE_PATH}5_Monitoring/5_Stacklight/13_Light_3",

    # 5_Production
    f"{MACHINE_PATH}5_Production/5_ActiveProgram",
    f"{MACHINE_PATH}5_Production/5_ActiveProgram/5_State",
    f"{MACHINE_PATH}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_1",
    f"{MACHINE_PATH}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_2",
    f"{MACHINE_PATH}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_3",

    # Notification
    f"{MACHINE_PATH}5_Notification/5_Prognoses/Maintenance",
    f"{MACHINE_PATH}5_Notification/5_Prognoses/Manual",
    f"{MACHINE_PATH}5_Notification/5_Prognoses/ToolChange",
]

# Datei für jedes Topic anlegen
for topic in topics:
    full_path = os.path.join(BASE_PATH, "data", PUBLISHER_ID, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write("")

for topic in topics:
    full_path = os.path.join(BASE_PATH, "metadata", PUBLISHER_ID, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write("")

# Status Topic
status_dir = os.path.join(BASE_PATH, "opcua/json/status", PUBLISHER_ID)
os.makedirs(status_dir, exist_ok=True)
with open(os.path.join(status_dir, "payload.json"), "w", encoding="utf-8") as f:
    f.write("")

# Connection Topic
connection_dir = os.path.join(BASE_PATH, "opcua/json/connection", PUBLISHER_ID)
os.makedirs(connection_dir, exist_ok=True)
with open(os.path.join(connection_dir, "payload.json"), "w", encoding="utf-8") as f:
    f.write("")

print("Leere Dateien erfolgreich angelegt.")
