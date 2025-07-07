""" Module that installs topics"""
import os

# Basis-Konfiguration
PublisherId = "example_publisher_1"
Uns = "Uns"
MachineName = "FullMachineTool"
BasePath = "umati/v3/"
MachineBasePath = f"{Uns}/machines/{MachineName}/"

# Liste aller relevanten Topics relativ zur Maschine
topics = [
    # 2_Identification
    f"{MachineBasePath}2_Identification",
    f"{MachineBasePath}2_Identification/5_SoftwareIdentification"

    # 5_Equipment
    f"{MachineBasePath}5_Equipment/5_Tools/13_Multi_1/13_SubTool_0/5_ToolLife/13_Rotations",
    f"{MachineBasePath}5_Equipment/5_Tools/13_Multi_1/13_SubTool_1/5_ToolLife/13_Rotations",
    f"{MachineBasePath}5_Equipment/5_Tools/13_Multi_1/13_SubTool_2/5_ToolLife/13_Rotations",
    f"{MachineBasePath}5_Equipment/5_Tools/13_Tool1/13_ToolLife/13_Rotations",

    # 5_Monitoring
    f"{MachineBasePath}5_Monitoring/13_Channel_1",
    f"{MachineBasePath}5_Monitoring/13_Channel_2",
    f"{MachineBasePath}5_Monitoring/13_Channel_3",
    f"{MachineBasePath}5_Monitoring/13_Channel_4",
    f"{MachineBasePath}5_Monitoring/13_Spindle_1",
    f"{MachineBasePath}5_Monitoring/13_EDM",
    f"{MachineBasePath}5_Monitoring/13_Laser",
    f"{MachineBasePath}5_Monitoring/5_Stacklight/13_Light_0",
    f"{MachineBasePath}5_Monitoring/5_Stacklight/13_Light_1",
    f"{MachineBasePath}5_Monitoring/5_Stacklight/13_Light_2",
    f"{MachineBasePath}5_Monitoring/5_Stacklight/13_Light_3",

    # 5_Production
    f"{MachineBasePath}5_Production/5_ActiveProgram",
    f"{MachineBasePath}5_Production/5_ActiveProgram/5_State",
    f"{MachineBasePath}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_1",
    f"{MachineBasePath}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_2",
    f"{MachineBasePath}5_Production/5_ProductionPlan/13_MyJob_1/PartSets/Set1/PartsPerRun/Part_3",

    # Notification
    f"{MachineBasePath}5_Notification/5_Prognoses/Maintenance",
    f"{MachineBasePath}5_Notification/5_Prognoses/Manual",
    f"{MachineBasePath}5_Notification/5_Prognoses/ToolChange",
]

# Datei für jedes Topic anlegen
for topic in topics:
    full_path = os.path.join(BasePath, "data", PublisherId, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w") as f:
        f.write("")

for topic in topics:
    full_path = os.path.join(BasePath, "metadata", PublisherId, topic)
    os.makedirs(full_path, exist_ok=True)
    file_path = os.path.join(full_path, "payload.json")
    with open(file_path, "w") as f:
        f.write("")

# Status Topic
status_dir = os.path.join(BasePath, "opcua/json/status", PublisherId)
os.makedirs(status_dir, exist_ok=True)
with open(os.path.join(status_dir, "payload.json"), "w") as f:
    f.write("")

# Connection Topic
connection_dir = os.path.join(BasePath, "opcua/json/connection", PublisherId)
os.makedirs(connection_dir, exist_ok=True)
with open(os.path.join(connection_dir, "payload.json"), "w") as f:
    f.write("")

print("Leere Dateien erfolgreich angelegt.")
