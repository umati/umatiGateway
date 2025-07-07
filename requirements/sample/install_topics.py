import os

# Basis-Konfiguration
publisher_id = "example_publisher_1"
uns = "uns"
machine_name = "FullMachineTool"
base_path = "umati/v3/"

# Liste aller relevanten Topics relativ zur Maschine
topics = [
    # 2_Identification
    f"{uns}/machines/{machine_name}/2_Identification",
    f"{uns}/machines/{machine_name}/2_Identification/5_SoftwareIdentification"

    # 5_Equipment
    f"{uns}/machines/{machine_name}/5_Equipment/5_Tools/13_Multi_1/13_SubTool_0/5_ToolLife/13_Rotations",
    f"{uns}/machines/{machine_name}/5_Equipment/5_Tools/13_Multi_1/13_SubTool_1/5_ToolLife/13_Rotations",
    f"{uns}/machines/{machine_name}/5_Equipment/5_Tools/13_Multi_1/13_SubTool_2/5_ToolLife/13_Rotations",
    f"{uns}/machines/{machine_name}/5_Equipment/5_Tools/13_Tool1/13_ToolLife/13_Rotations",

    # 5_Monitoring
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Channel_1",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Channel_2",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Channel_3",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Channel_4",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Spindle_1",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_EDM",
    f"{uns}/machines/{machine_name}/5_Monitoring/13_Laser",
    f"{uns}/machines/{machine_name}/5_Monitoring/5_Stacklight/13_Light_0",
    f"{uns}/machines/{machine_name}/5_Monitoring/5_Stacklight/13_Light_1",
    f"{uns}/machines/{machine_name}/5_Monitoring/5_Stacklight/13_Light_2",
    f"{uns}/machines/{machine_name}/5_Monitoring/5_Stacklight/13_Light_3",

    # 5_Production
    f"{uns}/machines/{machine_name}/5_Production/5_ActiveProgram",
    f"{uns}/machines/{machine_name}/5_Production/5_ActiveProgram/5_State",
    f"{uns}/machines/{machine_name}/5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_1",
    f"{uns}/machines/{machine_name}/5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_2",
    f"{uns}/machines/{machine_name}/5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_3",

    # Notification
    f"{uns}/machines/{machine_name}/5_Notification/5_Prognoses/Maintenance",
    f"{uns}/machines/{machine_name}/5_Notification/5_Prognoses/Manual",
    f"{uns}/machines/{machine_name}/5_Notification/5_Prognoses/ToolChange",
]

# Datei für jedes Topic anlegen
for topic in topics:
    full_path = os.path.join(base_path, "data", publisher_id, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w") as f:
        f.write("")

for topic in topics:
    full_path = os.path.join(base_path, "metadata", publisher_id, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w") as f:
        f.write("")

# Status Topic
status_dir = os.path.join(base_path, "opcua/json/status", publisher_id)
os.makedirs(status_dir, exist_ok=True)
with open(os.path.join(status_dir, "payload.json"), "w") as f:
    f.write("")

# Connection Topic
connection_dir = os.path.join(base_path, "opcua/json/connection", publisher_id)
os.makedirs(connection_dir, exist_ok=True)
with open(os.path.join(connection_dir, "payload.json"), "w") as f:
    f.write("")

print("Leere Dateien erfolgreich angelegt.")
