// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2025 FVA GmbH - interop4x. All rights reserved.
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using System.Text;
using System.Xml;
//ToDo make deep copies in the accessor methods
namespace UmatiGateway.OPC
{
    public class TypeDictionaries
    {
        public Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass> generatedDataTypes = new Dictionary<GeneratedDataTypeDefinition, GeneratedDataClass>(new DataClassComparer());
        private UmatiGatewayApp client;
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private List<string> errorMemmory = new List<string>();
        private Dictionary<NodeId, Node> opcBinary = new Dictionary<NodeId, Node>(new NodeIdComparer());
        private Dictionary<NodeId, Node> dataTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> eventTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> interfaceTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> objectTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> referenceTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> variableTypes = new Dictionary<NodeId, Node>();
        private Dictionary<NodeId, Node> xmlSchema = new Dictionary<NodeId, Node>();
        public List<DataTypeDefinition> dataTypeDefinition = new List<DataTypeDefinition>();

        public Boolean ReadExtraLibs = true;
        public String DI = "<opc:TypeDictionary\r\n  xmlns:opc=\"http://opcfoundation.org/BinarySchema/\"\r\n  xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\r\n  xmlns:ua=\"http://opcfoundation.org/UA/\"\r\n  xmlns:tns=\"http://opcfoundation.org/UA/DI/\"\r\n  DefaultByteOrder=\"LittleEndian\"\r\n  TargetNamespace=\"http://opcfoundation.org/UA/DI/\"\r\n>\r\n  <opc:Import Namespace=\"http://opcfoundation.org/UA/\" Location=\"Opc.Ua.BinarySchema.bsd\"/>\r\n\r\n  <opc:EnumeratedType Name=\"DeviceHealthEnumeration\" LengthInBits=\"32\">\r\n    <opc:EnumeratedValue Name=\"NORMAL\" Value=\"0\" />\r\n    <opc:EnumeratedValue Name=\"FAILURE\" Value=\"1\" />\r\n    <opc:EnumeratedValue Name=\"CHECK_FUNCTION\" Value=\"2\" />\r\n    <opc:EnumeratedValue Name=\"OFF_SPEC\" Value=\"3\" />\r\n    <opc:EnumeratedValue Name=\"MAINTENANCE_REQUIRED\" Value=\"4\" />\r\n  </opc:EnumeratedType>\r\n\r\n  <opc:StructuredType Name=\"FetchResultDataType\" BaseType=\"ua:ExtensionObject\">\r\n  </opc:StructuredType>\r\n\r\n  <opc:StructuredType Name=\"TransferResultErrorDataType\" BaseType=\"tns:FetchResultDataType\">\r\n    <opc:Field Name=\"Status\" TypeName=\"opc:Int32\" />\r\n    <opc:Field Name=\"Diagnostics\" TypeName=\"ua:DiagnosticInfo\" />\r\n  </opc:StructuredType>\r\n\r\n  <opc:StructuredType Name=\"TransferResultDataDataType\" BaseType=\"tns:FetchResultDataType\">\r\n    <opc:Field Name=\"SequenceNumber\" TypeName=\"opc:Int32\" />\r\n    <opc:Field Name=\"EndOfResults\" TypeName=\"opc:Boolean\" />\r\n    <opc:Field Name=\"NoOfParameterDefs\" TypeName=\"opc:Int32\" />\r\n    <opc:Field Name=\"ParameterDefs\" TypeName=\"tns:ParameterResultDataType\" LengthField=\"NoOfParameterDefs\" />\r\n  </opc:StructuredType>\r\n\r\n  <opc:StructuredType Name=\"ParameterResultDataType\" BaseType=\"ua:ExtensionObject\">\r\n    <opc:Field Name=\"NoOfNodePath\" TypeName=\"opc:Int32\" />\r\n    <opc:Field Name=\"NodePath\" TypeName=\"ua:QualifiedName\" LengthField=\"NoOfNodePath\" />\r\n    <opc:Field Name=\"StatusCode\" TypeName=\"ua:StatusCode\" />\r\n    <opc:Field Name=\"Diagnostics\" TypeName=\"ua:DiagnosticInfo\" />\r\n  </opc:StructuredType>\r\n\r\n  <opc:EnumeratedType Name=\"SoftwareVersionFileType\" LengthInBits=\"32\">\r\n    <opc:EnumeratedValue Name=\"Current\" Value=\"0\" />\r\n    <opc:EnumeratedValue Name=\"Pending\" Value=\"1\" />\r\n    <opc:EnumeratedValue Name=\"Fallback\" Value=\"2\" />\r\n  </opc:EnumeratedType>\r\n\r\n  <opc:EnumeratedType Name=\"UpdateBehavior\" LengthInBits=\"32\" IsOptionSet=\"true\">\r\n    <opc:EnumeratedValue Name=\"None\" Value=\"0\" />\r\n    <opc:EnumeratedValue Name=\"KeepsParameters\" Value=\"1\" />\r\n    <opc:EnumeratedValue Name=\"WillDisconnect\" Value=\"2\" />\r\n    <opc:EnumeratedValue Name=\"RequiresPowerCycle\" Value=\"4\" />\r\n    <opc:EnumeratedValue Name=\"WillReboot\" Value=\"8\" />\r\n    <opc:EnumeratedValue Name=\"NeedsPreparation\" Value=\"16\" />\r\n  </opc:EnumeratedType>\r\n\r\n</opc:TypeDictionary>";
        public String Machinery_Jobs = "<opc:TypeDictionary xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:tns=\"http://opcfoundation.org/UA/Machinery/Jobs/\" DefaultByteOrder=\"LittleEndian\" xmlns:opc=\"http://opcfoundation.org/BinarySchema/\" xmlns:ns1=\"http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/\" xmlns:ua=\"http://opcfoundation.org/UA/\" TargetNamespace=\"http://opcfoundation.org/UA/Machinery/Jobs/\">\r\n <opc:Import Namespace=\"http://opcfoundation.org/UA/\"/>\r\n <opc:Import Namespace=\"http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/\"/>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"BOMComponentInformationDataType\">\r\n  <opc:Field TypeName=\"tns:OutputInformationDataType\" Name=\"Identification\"/>\r\n  <opc:Field TypeName=\"opc:Double\" Name=\"Quantity\"/>\r\n  <opc:Field TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"BOMInformationDataType\">\r\n  <opc:Field TypeName=\"tns:OutputInformationDataType\" Name=\"Identification\"/>\r\n  <opc:Field TypeName=\"opc:Int32\" Name=\"NoOfComponentInformation\"/>\r\n  <opc:Field LengthField=\"NoOfComponentInformation\" TypeName=\"tns:BOMComponentInformationDataType\" Name=\"ComponentInformation\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"OutputInformationDataType\">\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"OrderNumberSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"LotNumberSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"SerialNumberSpecified\"/>\r\n  <opc:Field Length=\"29\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ItemNumber\"/>\r\n  <opc:Field TypeName=\"tns:OutputInfoType\" Name=\"OutputInfo\"/>\r\n  <opc:Field SwitchField=\"OrderNumberSpecified\" TypeName=\"opc:CharArray\" Name=\"OrderNumber\"/>\r\n  <opc:Field SwitchField=\"LotNumberSpecified\" TypeName=\"opc:CharArray\" Name=\"LotNumber\"/>\r\n  <opc:Field SwitchField=\"SerialNumberSpecified\" TypeName=\"opc:CharArray\" Name=\"SerialNumber\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"OutputPerformanceInfoDataType\">\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"StartTimeSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EndTimeSpecified\"/>\r\n  <opc:Field Length=\"30\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"tns:OutputInformationDataType\" Name=\"Identification\"/>\r\n  <opc:Field SwitchField=\"StartTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"StartTime\"/>\r\n  <opc:Field SwitchField=\"EndTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"EndTime\"/>\r\n  <opc:Field TypeName=\"opc:Int32\" Name=\"NoOfParameters\"/>\r\n  <opc:Field LengthField=\"NoOfParameters\" TypeName=\"ns1:ISA95ParameterDataType\" Name=\"Parameters\"/>\r\n </opc:StructuredType>\r\n <opc:EnumeratedType LengthInBits=\"32\" Name=\"JobExecutionMode\">\r\n  <opc:EnumeratedValue Name=\"SimulationMode\" Value=\"0\"/>\r\n  <opc:EnumeratedValue Name=\"TestMode\" Value=\"1\"/>\r\n  <opc:EnumeratedValue Name=\"ProductionMode\" Value=\"2\"/>\r\n </opc:EnumeratedType>\r\n <opc:EnumeratedType LengthInBits=\"32\" Name=\"JobResult\">\r\n  <opc:EnumeratedValue Name=\"Unknown\" Value=\"0\"/>\r\n  <opc:EnumeratedValue Name=\"Successful\" Value=\"1\"/>\r\n  <opc:EnumeratedValue Name=\"Unsuccessful\" Value=\"2\"/>\r\n </opc:EnumeratedType>\r\n <opc:EnumeratedType LengthInBits=\"32\" Name=\"ProcessIrregularity\">\r\n  <opc:EnumeratedValue Name=\"CapabilityUnavailable\" Value=\"0\"/>\r\n  <opc:EnumeratedValue Name=\"Detected\" Value=\"1\"/>\r\n  <opc:EnumeratedValue Name=\"NotDetected\" Value=\"2\"/>\r\n  <opc:EnumeratedValue Name=\"NotYetDetermined\" Value=\"3\"/>\r\n </opc:EnumeratedType>\r\n <opc:EnumeratedType LengthInBits=\"8\" Name=\"OutputInfoType\" IsOptionSet=\"true\">\r\n  <opc:EnumeratedValue Name=\"OrderNumber\" Value=\"0\"/>\r\n  <opc:EnumeratedValue Name=\"LotNumber\" Value=\"1\"/>\r\n  <opc:EnumeratedValue Name=\"SerialNumber\" Value=\"2\"/>\r\n </opc:EnumeratedType>\r\n</opc:TypeDictionary>";
        public String JobControl = "<opc:TypeDictionary xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:tns=\"http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/\" DefaultByteOrder=\"LittleEndian\" xmlns:opc=\"http://opcfoundation.org/BinarySchema/\" xmlns:ua=\"http://opcfoundation.org/UA/\" TargetNamespace=\"http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/\">\r\n <opc:Import Namespace=\"http://opcfoundation.org/UA/\"/>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95EquipmentDataType\">\r\n  <opc:Documentation>Defines an equipment resource or a piece of equipment, a quantity, an optional description, and an optional collection of properties.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EquipmentUseSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"QuantitySpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PropertiesSpecified\"/>\r\n  <opc:Field Length=\"27\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"EquipmentUseSpecified\" TypeName=\"opc:CharArray\" Name=\"EquipmentUse\"/>\r\n  <opc:Field SwitchField=\"QuantitySpecified\" TypeName=\"opc:CharArray\" Name=\"Quantity\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"PropertiesSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfProperties\"/>\r\n  <opc:Field LengthField=\"NoOfProperties\" SwitchField=\"PropertiesSpecified\" TypeName=\"tns:ISA95PropertyDataType\" Name=\"Properties\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95JobOrderAndStateDataType\">\r\n  <opc:Documentation>Defines the information needed to schedule and execute a job.</opc:Documentation>\r\n  <opc:Field TypeName=\"tns:ISA95JobOrderDataType\" Name=\"JobOrder\"/>\r\n  <opc:Field TypeName=\"opc:Int32\" Name=\"NoOfState\"/>\r\n  <opc:Field LengthField=\"NoOfState\" TypeName=\"tns:ISA95StateDataType\" Name=\"State\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95JobOrderDataType\">\r\n  <opc:Documentation>Defines the information needed to schedule and execute a job.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"WorkMasterIDSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"StartTimeSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EndTimeSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PrioritySpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"JobOrderParametersSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PersonnelRequirementsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EquipmentRequirementsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PhysicalAssetRequirementsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialRequirementsSpecified\"/>\r\n  <opc:Field Length=\"22\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"JobOrderID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"WorkMasterIDSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfWorkMasterID\"/>\r\n  <opc:Field LengthField=\"NoOfWorkMasterID\" SwitchField=\"WorkMasterIDSpecified\" TypeName=\"tns:ISA95WorkMasterDataType\" Name=\"WorkMasterID\"/>\r\n  <opc:Field SwitchField=\"StartTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"StartTime\"/>\r\n  <opc:Field SwitchField=\"EndTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"EndTime\"/>\r\n  <opc:Field SwitchField=\"PrioritySpecified\" TypeName=\"opc:Int16\" Name=\"Priority\"/>\r\n  <opc:Field SwitchField=\"JobOrderParametersSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfJobOrderParameters\"/>\r\n  <opc:Field LengthField=\"NoOfJobOrderParameters\" SwitchField=\"JobOrderParametersSpecified\" TypeName=\"tns:ISA95ParameterDataType\" Name=\"JobOrderParameters\"/>\r\n  <opc:Field SwitchField=\"PersonnelRequirementsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfPersonnelRequirements\"/>\r\n  <opc:Field LengthField=\"NoOfPersonnelRequirements\" SwitchField=\"PersonnelRequirementsSpecified\" TypeName=\"tns:ISA95PersonnelDataType\" Name=\"PersonnelRequirements\"/>\r\n  <opc:Field SwitchField=\"EquipmentRequirementsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfEquipmentRequirements\"/>\r\n  <opc:Field LengthField=\"NoOfEquipmentRequirements\" SwitchField=\"EquipmentRequirementsSpecified\" TypeName=\"tns:ISA95EquipmentDataType\" Name=\"EquipmentRequirements\"/>\r\n  <opc:Field SwitchField=\"PhysicalAssetRequirementsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfPhysicalAssetRequirements\"/>\r\n  <opc:Field LengthField=\"NoOfPhysicalAssetRequirements\" SwitchField=\"PhysicalAssetRequirementsSpecified\" TypeName=\"tns:ISA95PhysicalAssetDataType\" Name=\"PhysicalAssetRequirements\"/>\r\n  <opc:Field SwitchField=\"MaterialRequirementsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfMaterialRequirements\"/>\r\n  <opc:Field LengthField=\"NoOfMaterialRequirements\" SwitchField=\"MaterialRequirementsSpecified\" TypeName=\"tns:ISA95MaterialDataType\" Name=\"MaterialRequirements\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95JobResponseDataType\">\r\n  <opc:Documentation>Defines the information needed to schedule and execute a job.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"StartTimeSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EndTimeSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"JobResponseDataSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PersonnelActualsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EquipmentActualsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PhysicalAssetActualsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialActualsSpecified\"/>\r\n  <opc:Field Length=\"24\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"JobResponseID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"JobOrderID\"/>\r\n  <opc:Field SwitchField=\"StartTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"StartTime\"/>\r\n  <opc:Field SwitchField=\"EndTimeSpecified\" TypeName=\"opc:DateTime\" Name=\"EndTime\"/>\r\n  <opc:Field TypeName=\"opc:Int32\" Name=\"NoOfJobState\"/>\r\n  <opc:Field LengthField=\"NoOfJobState\" TypeName=\"tns:ISA95StateDataType\" Name=\"JobState\"/>\r\n  <opc:Field SwitchField=\"JobResponseDataSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfJobResponseData\"/>\r\n  <opc:Field LengthField=\"NoOfJobResponseData\" SwitchField=\"JobResponseDataSpecified\" TypeName=\"tns:ISA95ParameterDataType\" Name=\"JobResponseData\"/>\r\n  <opc:Field SwitchField=\"PersonnelActualsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfPersonnelActuals\"/>\r\n  <opc:Field LengthField=\"NoOfPersonnelActuals\" SwitchField=\"PersonnelActualsSpecified\" TypeName=\"tns:ISA95PersonnelDataType\" Name=\"PersonnelActuals\"/>\r\n  <opc:Field SwitchField=\"EquipmentActualsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfEquipmentActuals\"/>\r\n  <opc:Field LengthField=\"NoOfEquipmentActuals\" SwitchField=\"EquipmentActualsSpecified\" TypeName=\"tns:ISA95EquipmentDataType\" Name=\"EquipmentActuals\"/>\r\n  <opc:Field SwitchField=\"PhysicalAssetActualsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfPhysicalAssetActuals\"/>\r\n  <opc:Field LengthField=\"NoOfPhysicalAssetActuals\" SwitchField=\"PhysicalAssetActualsSpecified\" TypeName=\"tns:ISA95PhysicalAssetDataType\" Name=\"PhysicalAssetActuals\"/>\r\n  <opc:Field SwitchField=\"MaterialActualsSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfMaterialActuals\"/>\r\n  <opc:Field LengthField=\"NoOfMaterialActuals\" SwitchField=\"MaterialActualsSpecified\" TypeName=\"tns:ISA95MaterialDataType\" Name=\"MaterialActuals\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95MaterialDataType\">\r\n  <opc:Documentation>Defines a material resource, a quantity, an optional description, and an optional collection of properties.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialClassIDSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialDefinitionIDSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialLotIDSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialSublotIDSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"MaterialUseSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"QuantitySpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PropertiesSpecified\"/>\r\n  <opc:Field Length=\"23\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field SwitchField=\"MaterialClassIDSpecified\" TypeName=\"opc:CharArray\" Name=\"MaterialClassID\"/>\r\n  <opc:Field SwitchField=\"MaterialDefinitionIDSpecified\" TypeName=\"opc:CharArray\" Name=\"MaterialDefinitionID\"/>\r\n  <opc:Field SwitchField=\"MaterialLotIDSpecified\" TypeName=\"opc:CharArray\" Name=\"MaterialLotID\"/>\r\n  <opc:Field SwitchField=\"MaterialSublotIDSpecified\" TypeName=\"opc:CharArray\" Name=\"MaterialSublotID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"MaterialUseSpecified\" TypeName=\"opc:CharArray\" Name=\"MaterialUse\"/>\r\n  <opc:Field SwitchField=\"QuantitySpecified\" TypeName=\"opc:CharArray\" Name=\"Quantity\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"PropertiesSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfProperties\"/>\r\n  <opc:Field LengthField=\"NoOfProperties\" SwitchField=\"PropertiesSpecified\" TypeName=\"tns:ISA95PropertyDataType\" Name=\"Properties\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95ParameterDataType\">\r\n  <opc:Documentation>A subtype of OPC UA Structure that defines three linked data items: the ID, which is a unique identifier for a property, the value, which is the data that is identified, and an optional description of the parameter.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"SubparametersSpecified\"/>\r\n  <opc:Field Length=\"29\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field TypeName=\"ua:Variant\" Name=\"Value\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"SubparametersSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfSubparameters\"/>\r\n  <opc:Field LengthField=\"NoOfSubparameters\" SwitchField=\"SubparametersSpecified\" TypeName=\"tns:ISA95ParameterDataType\" Name=\"Subparameters\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95PersonnelDataType\">\r\n  <opc:Documentation>Defines a personnel resource or a person, a quantity, an optional description, and an optional collection of properties.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PersonnelUseSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"QuantitySpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PropertiesSpecified\"/>\r\n  <opc:Field Length=\"27\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"PersonnelUseSpecified\" TypeName=\"opc:CharArray\" Name=\"PersonnelUse\"/>\r\n  <opc:Field SwitchField=\"QuantitySpecified\" TypeName=\"opc:CharArray\" Name=\"Quantity\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"PropertiesSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfProperties\"/>\r\n  <opc:Field LengthField=\"NoOfProperties\" SwitchField=\"PropertiesSpecified\" TypeName=\"tns:ISA95PropertyDataType\" Name=\"Properties\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95PhysicalAssetDataType\">\r\n  <opc:Documentation>Defines a physical asset, a quantity, an optional description, and an optional collection of properties.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PhysicalAssetUseSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"QuantitySpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"PropertiesSpecified\"/>\r\n  <opc:Field Length=\"27\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"PhysicalAssetUseSpecified\" TypeName=\"opc:CharArray\" Name=\"PhysicalAssetUse\"/>\r\n  <opc:Field SwitchField=\"QuantitySpecified\" TypeName=\"opc:CharArray\" Name=\"Quantity\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"PropertiesSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfProperties\"/>\r\n  <opc:Field LengthField=\"NoOfProperties\" SwitchField=\"PropertiesSpecified\" TypeName=\"tns:ISA95PropertyDataType\" Name=\"Properties\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95PropertyDataType\">\r\n  <opc:Documentation>A subtype of OPC UA Structure that defines two linked data items: an ID, which is a unique identifier for a property within the scope of the associated resource, and the value, which is the data for the property.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"EngineeringUnitsSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"SubpropertiesSpecified\"/>\r\n  <opc:Field Length=\"29\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field TypeName=\"ua:Variant\" Name=\"Value\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfDescription\"/>\r\n  <opc:Field LengthField=\"NoOfDescription\" SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"EngineeringUnitsSpecified\" TypeName=\"ua:EUInformation\" Name=\"EngineeringUnits\"/>\r\n  <opc:Field SwitchField=\"SubpropertiesSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfSubproperties\"/>\r\n  <opc:Field LengthField=\"NoOfSubproperties\" SwitchField=\"SubpropertiesSpecified\" TypeName=\"tns:ISA95PropertyDataType\" Name=\"Subproperties\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95StateDataType\">\r\n  <opc:Documentation>Defines the information needed to schedule and execute a job.</opc:Documentation>\r\n  <opc:Field TypeName=\"ua:RelativePath\" Name=\"BrowsePath\"/>\r\n  <opc:Field TypeName=\"ua:LocalizedText\" Name=\"StateText\"/>\r\n  <opc:Field TypeName=\"opc:UInt32\" Name=\"StateNumber\"/>\r\n </opc:StructuredType>\r\n <opc:StructuredType BaseType=\"ua:ExtensionObject\" Name=\"ISA95WorkMasterDataType\">\r\n  <opc:Documentation>Defines a Work Master ID and the defined parameters for the Work Master.</opc:Documentation>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"DescriptionSpecified\"/>\r\n  <opc:Field TypeName=\"opc:Bit\" Name=\"ParametersSpecified\"/>\r\n  <opc:Field Length=\"30\" TypeName=\"opc:Bit\" Name=\"Reserved1\"/>\r\n  <opc:Field TypeName=\"opc:CharArray\" Name=\"ID\"/>\r\n  <opc:Field SwitchField=\"DescriptionSpecified\" TypeName=\"ua:LocalizedText\" Name=\"Description\"/>\r\n  <opc:Field SwitchField=\"ParametersSpecified\" TypeName=\"opc:Int32\" Name=\"NoOfParameters\"/>\r\n  <opc:Field LengthField=\"NoOfParameters\" SwitchField=\"ParametersSpecified\" TypeName=\"tns:ISA95ParameterDataType\" Name=\"Parameters\"/>\r\n </opc:StructuredType>\r\n</opc:TypeDictionary>";
        public TypeDictionaries(UmatiGatewayApp Client)
        {
            this.client = Client;
        }

        public void ReadTypeDictionary(bool onlyBinaries)
        {
            this.ReadOpcBinary();
            if (!onlyBinaries)
            {
                Console.WriteLine("Read DataTypes");
                this.ReadDataTypes();
                Console.WriteLine("Read EventTypes");
                this.ReadEventTypes();
                Console.WriteLine("ReadInterfaceTypes");
                this.ReadInterfaceTypes();
                Console.WriteLine("ReadObjectTypes");
                this.ReadObjectTypes();
                Console.WriteLine("ReadReferenceTypes");
                this.ReadReferenceTypes();
                Console.WriteLine("VariableTypes");
                this.ReadVariableTypes();
            }
            Console.WriteLine("TypeDictionary Read Finished");
        }
        public void ReadOpcBinary()
        {
            List<NodeId> binaryTypeDictionaries = new List<NodeId>();
            binaryTypeDictionaries = this.client.BrowseLocalNodeIdsWithTypeDefinition(ObjectIds.OPCBinarySchema_TypeSystem, BrowseDirection.Forward, (uint)NodeClass.Variable, ReferenceTypeIds.HasComponent, true, VariableTypeIds.DataTypeDictionaryType);
            foreach (NodeId binaryTypeDictionary in binaryTypeDictionaries)
            {
                DataValue? dv = this.client.ReadValue(binaryTypeDictionary);
                if (dv != null)
                {
                    string xmlString = Encoding.UTF8.GetString((byte[])dv.Value);
                    this.generateDataClasses(xmlString);
                }
                else
                {
                    Logger.Error($"Unable to read binaryTypeDictionary {binaryTypeDictionary}");
                }
            };
            if (ReadExtraLibs)
            {
                this.generateDataClasses(DI);
                this.generateDataClasses(Machinery_Jobs);
                this.generateDataClasses(JobControl);
            }
            List<NodeId> opcBinaryNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectIds.OPCBinarySchema_TypeSystem, NodeClass.Variable, opcBinaryNodeIds, ReferenceTypeIds.HasComponent);
            this.ReadAndAppendTypeNodeIds(ObjectIds.OPCBinarySchema_TypeSystem, NodeClass.Variable, opcBinaryNodeIds, ReferenceTypeIds.HasProperty);
            Dictionary<NodeId, Node> opcBinaryTypes = new Dictionary<NodeId, Node>();
            opcBinaryNodeIds = opcBinaryNodeIds.Distinct().ToList();
            foreach (NodeId opcBinaryNodeId in opcBinaryNodeIds)
            {
                Node? node = this.client.ReadNode(opcBinaryNodeId);
                if (node != null)
                {
                    opcBinaryTypes.Add(opcBinaryNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", opcBinaryNodeId);
                }
            }
            this.SetOpcBinaryTypes(opcBinaryTypes);
        }
        private void ReadAndAppendTypeNodeIds(NodeId nodeId, NodeClass nodeClass, List<NodeId> nodeIds)
        {
            nodeIds.Add(nodeId);
            List<NodeId> subTypeNodeIds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (uint)nodeClass, ReferenceTypeIds.HasSubtype, true);
            foreach (NodeId subTypeNodeId in subTypeNodeIds)
            {
                this.ReadAndAppendTypeNodeIds(subTypeNodeId, nodeClass, nodeIds);
            }
        }
        private void ReadAndAppendTypeNodeIds(NodeId nodeId, NodeClass nodeClass, List<NodeId> nodeIds, NodeId referenceTypeId)
        {
            nodeIds.Add(nodeId);
            List<NodeId> subTypeNodeIds = this.client.BrowseLocalNodeIds(nodeId, BrowseDirection.Forward, (uint)nodeClass, referenceTypeId, true);
            foreach (NodeId subTypeNodeId in subTypeNodeIds)
            {
                this.ReadAndAppendTypeNodeIds(subTypeNodeId, nodeClass, nodeIds, referenceTypeId);
            }
        }
        private void generateDataClasses(string xmlString)
        {
            Console.Out.WriteLine(xmlString);
            XmlTextReader reader = new XmlTextReader(new System.IO.StringReader(xmlString));
            GeneratedStructure generatedStructure = new GeneratedStructure();
            GeneratedEnumeratedType generatedEnumeratedType = new GeneratedEnumeratedType();
            GeneratedOpaqueType generatedOpaqueType = new GeneratedOpaqueType();
            //Structure or enumerated Type
            GeneratedComplexTypes generatedComplexType = GeneratedComplexTypes.StructuredType;
            string? Name = null;
            string? BaseType = null;
            string documentation = "";
            string? targetNamespace = null;
            string? xsi = null;
            string? tns = null;
            string? DefaultByteorder = null;
            string? opc = null;
            string? ua = null;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        string nodeElement = reader.Name;
                        switch (nodeElement)
                        {
                            case ("opc:StructuredType"):
                                generatedComplexType = GeneratedComplexTypes.StructuredType;
                                generatedStructure = new GeneratedStructure();
                                Name = reader.GetAttribute("Name");
                                BaseType = reader.GetAttribute("BaseType");
                                generatedStructure.BaseType = BaseType;
                                if (Name != null)
                                {
                                    generatedStructure.Name = Name;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the structure is null");
                                }
                                break;
                            case ("opc:Documentation"):
                                documentation = reader.ReadInnerXml();
                                if (generatedComplexType == GeneratedComplexTypes.StructuredType)
                                {
                                    generatedStructure.Documentation = documentation;
                                }
                                else if (generatedComplexType == GeneratedComplexTypes.EnumeratedType)
                                {
                                    generatedEnumeratedType.Documentation = documentation;
                                }
                                else if (generatedComplexType == GeneratedComplexTypes.OpaqueType)
                                {

                                }
                                break;
                            case ("opc:Field"):
                                GeneratedField generatedField = new GeneratedField();
                                string? typeName = reader.GetAttribute("TypeName");
                                if (typeName != null)
                                {
                                    generatedField.TypeName = typeName;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The TypeName of the Field is null");
                                }
                                string? fieldname = reader.GetAttribute("Name");
                                if (fieldname != null)
                                {
                                    generatedField.Name = fieldname;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the Field is null");
                                }
                                string? lengthField = reader.GetAttribute("LengthField");
                                if (lengthField != null)
                                {
                                    generatedField.IsLengthField = true;
                                    generatedField.LengthField = lengthField;
                                }
                                string? length = reader.GetAttribute("Length");
                                if (length != null)
                                {
                                    generatedField.HasLength = true;
                                    generatedField.Length = UInt32.Parse(length);
                                }
                                string? switchfield = reader.GetAttribute("SwitchField");
                                if (switchfield != null)
                                {
                                    generatedField.IsSwitchField = true;
                                }
                                if (generatedComplexType == GeneratedComplexTypes.StructuredType)
                                {
                                    generatedStructure.fields.Add(generatedField);
                                }
                                else
                                {
                                    this.errorMemmory.Add("Trying to add a field to a non Structure.");
                                }
                                break;
                            case ("opc:EnumeratedType"):
                                generatedComplexType = GeneratedComplexTypes.EnumeratedType;
                                generatedEnumeratedType = new GeneratedEnumeratedType();
                                Name = reader.GetAttribute("Name");
                                if (Name != null)
                                {
                                    generatedEnumeratedType.Name = Name;
                                }
                                else
                                {
                                    this.errorMemmory.Add("The Name of the structure is null");
                                }
                                break;
                            case ("opc:EnumeratedValue"):
                                break;
                            case ("opc:OpaqueType"):
                                generatedComplexType = GeneratedComplexTypes.OpaqueType;
                                break;
                            case ("opc:TypeDictionary"):
                                targetNamespace = reader.GetAttribute("TargetNamespace");
                                if (targetNamespace == null)
                                {
                                    this.errorMemmory.Add("The TargetNameSpace for the Typedictionary is null.");
                                }
                                xsi = reader.GetAttribute("xmlns:xsi");
                                tns = reader.GetAttribute("xmlns:tns");
                                DefaultByteorder = reader.GetAttribute("DefaultByteOrder");
                                opc = reader.GetAttribute("xmlns:opc");
                                ua = reader.GetAttribute("xmlns:ua");

                                break;
                            case ("opc:Import"):
                                break;
                            default:
                                Console.WriteLine("UnknownType: -> ##################" + "###" + reader.Name + "###");
                                break;
                        }
                        //Console.WriteLine("###" + reader.Name + "###");

                        break;
                    case XmlNodeType.Text:
                        break;
                    case XmlNodeType.EndElement:
                        nodeElement = reader.Name;
                        switch (nodeElement)
                        {
                            case ("opc:StructuredType"):
                                if (targetNamespace != null)
                                {
                                    GeneratedDataTypeDefinition gdd = new GeneratedDataTypeDefinition(targetNamespace, generatedStructure.Name);
                                    gdd.xsi = xsi;
                                    gdd.tns = tns;
                                    gdd.DefaultByteOrder = DefaultByteorder;
                                    gdd.opc = opc;
                                    gdd.ua = ua;
                                    generatedStructure.DataTypeDefinition = gdd;
                                    this.generatedDataTypes.Add(gdd, generatedStructure);
                                }
                                break;
                        }
                        break;
                }
            }
            foreach (string error in this.errorMemmory)
            {
                Logger.Error(error);
            }
        }
        public void ReadDataTypes()
        {
            List<NodeId> dataTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(DataTypeIds.BaseDataType, NodeClass.DataType, dataTypeNodeIds);
            Dictionary<NodeId, Node> dataTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId dataTypeNodeId in dataTypeNodeIds)
            {
                Node? node = this.client.ReadNode(dataTypeNodeId);
                if (node != null)
                {
                    dataTypes.Add(dataTypeNodeId, node);
                    // Console.WriteLine(dataTypeNodeId);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", dataTypeNodeId);
                }
            }
            this.SetDataTypes(dataTypes);
        }
        public void ReadEventTypes()
        {
            List<NodeId> eventTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseEventType, NodeClass.ObjectType, eventTypeNodeIds);
            Dictionary<NodeId, Node> eventTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId eventTypeNodeId in eventTypeNodeIds)
            {
                Node? node = this.client.ReadNode(eventTypeNodeId);
                if (node != null)
                {
                    eventTypes.Add(eventTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", eventTypeNodeId);
                }
            }
            this.SetEventTypes(eventTypes);
        }
        public void ReadInterfaceTypes()
        {
            List<NodeId> interfaceTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseInterfaceType, NodeClass.ObjectType, interfaceTypeNodeIds);
            Dictionary<NodeId, Node> interfaceTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId interfaceTypeNodeId in interfaceTypeNodeIds)
            {
                Node? node = this.client.ReadNode(interfaceTypeNodeId);
                if (node != null)
                {
                    interfaceTypes.Add(interfaceTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", interfaceTypeNodeId);
                }
            }
            this.SetInterfaceTypes(interfaceTypes);
        }

        public void ReadObjectTypes()
        {
            List<NodeId> objectTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ObjectTypeIds.BaseObjectType, NodeClass.ObjectType, objectTypeNodeIds);
            Dictionary<NodeId, Node> objectTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId objectTypeNodeId in objectTypeNodeIds)
            {
                Node? node = this.client.ReadNode(objectTypeNodeId);
                if (node != null)
                {
                    objectTypes.Add(objectTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", objectTypeNodeId);
                }
            }
            this.SetObjectTypes(objectTypes);

        }
        public void ReadReferenceTypes()
        {
            List<NodeId> referenceTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(ReferenceTypeIds.References, NodeClass.ReferenceType, referenceTypeNodeIds);
            Dictionary<NodeId, Node> referenceTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId referenceTypeNodeId in referenceTypeNodeIds)
            {
                Node? node = this.client.ReadNode(referenceTypeNodeId);
                if (node != null)
                {
                    referenceTypes.Add(referenceTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", referenceTypeNodeId);
                }
            }
            this.SetReferenceTypes(referenceTypes);
        }

        public void ReadVariableTypes()
        {
            List<NodeId> variableTypeNodeIds = new List<NodeId>();
            this.ReadAndAppendTypeNodeIds(VariableTypeIds.BaseVariableType, NodeClass.VariableType, variableTypeNodeIds);
            Dictionary<NodeId, Node> variableTypes = new Dictionary<NodeId, Node>();
            foreach (NodeId variableTypeNodeId in variableTypeNodeIds)
            {
                Node? node = this.client.ReadNode(variableTypeNodeId);
                if (node != null)
                {
                    variableTypes.Add(variableTypeNodeId, node);
                }
                else
                {
                    Console.WriteLine("Error Reading Node for NodeId:", variableTypeNodeId);
                }
            }
            this.SetVariableTypes(variableTypes);
        }
        public void SetOpcBinaryTypes(Dictionary<NodeId, Node> opcBinary)
        {
            this.opcBinary.Clear();
            if (opcBinary != null)
            {
                this.opcBinary = opcBinary;
            }
        }
        public Dictionary<NodeId, Node> GetOpcBinary()
        {
            return this.opcBinary;
        }
        public void SetDataTypes(Dictionary<NodeId, Node> dataTypes)
        {
            this.dataTypes.Clear();
            if (dataTypes != null)
            {
                this.dataTypes = dataTypes;
            }
        }
        public Dictionary<NodeId, Node> GetDataTypes()
        {
            return this.dataTypes;
        }
        public void SetEventTypes(Dictionary<NodeId, Node> eventTypes)
        {
            this.eventTypes.Clear();
            if (eventTypes != null)
            {
                this.eventTypes = eventTypes;
            }
        }
        public Dictionary<NodeId, Node> GetEventTypes()
        {
            return this.eventTypes;
        }
        public void SetInterfaceTypes(Dictionary<NodeId, Node> interfaceTypes)
        {
            this.interfaceTypes.Clear();
            if (interfaceTypes != null)
            {
                this.interfaceTypes = interfaceTypes;
            }
        }
        public Dictionary<NodeId, Node> GetInterfaceTypes()
        {
            return this.interfaceTypes;
        }
        public void SetObjectTypes(Dictionary<NodeId, Node> objectTypes)
        {
            this.objectTypes.Clear();
            if (interfaceTypes != null)
            {
                this.objectTypes = objectTypes;
            }
        }
        public Dictionary<NodeId, Node> GetObjectTypes()
        {
            return this.objectTypes;
        }
        public void SetReferenceTypes(Dictionary<NodeId, Node> referenceTypes)
        {
            this.referenceTypes.Clear();
            if (referenceTypes != null)
            {
                this.referenceTypes = referenceTypes;
            }
        }
        public Dictionary<NodeId, Node> GetReferenceTypes()
        {
            return this.referenceTypes;
        }
        public void SetVariableTypes(Dictionary<NodeId, Node> variableTypes)
        {
            this.variableTypes.Clear();
            if (variableTypes != null)
            {
                this.variableTypes = variableTypes;
            }
        }
        public Dictionary<NodeId, Node> GetVariableTypes()
        {
            return this.variableTypes;
        }
        public Node? FindBinaryEncodingType(NodeId nodeId)
        {
            Node? encodingType = null;
            encodingType = this.opcBinary[nodeId];
            return encodingType;
        }
    }
    public class NodeIdComparer : IEqualityComparer<NodeId>
    {
        public bool Equals(NodeId? n1, NodeId? n2)
        {
            if (n1 == n2)
            {
                return true;
            }
            if (n1 == null || n2 == null)
            {
                return false;
            }
            return (n1.Identifier == n2.Identifier && n1.NamespaceIndex == n2.NamespaceIndex);
        }
        public int GetHashCode(NodeId n1)
        {
            return n1.Identifier.GetHashCode() + n1.NamespaceIndex.GetHashCode();
        }
    }
    public class DataClassComparer : IEqualityComparer<GeneratedDataTypeDefinition>
    {
        public bool Equals(GeneratedDataTypeDefinition? n1, GeneratedDataTypeDefinition? n2)
        {
            if (n1 == n2)
            {
                return true;
            }
            if (n1 == null || n2 == null)
            {
                return false;
            }
            return (n1.name == n2.name && n1.nameSpace == n2.nameSpace);
        }
        public int GetHashCode(GeneratedDataTypeDefinition n1)
        {
            return n1.name.GetHashCode() + n1.nameSpace.GetHashCode();
        }
    }
    public class GeneratedDataTypeDefinition
    {
        public string nameSpace;
        public string name;
        public string? xsi = null;
        public string? DefaultByteOrder = null;
        public string? opc = null;
        public string? ua = null;
        public string? tns = null;

        public GeneratedDataTypeDefinition(string nameSpace, string name)
        {
            this.nameSpace = nameSpace;
            this.name = name;
        }
    }
    public class GeneratedField
    {
        public bool IsLengthField = false;
        public bool HasLength = false;
        public bool IsSwitchField = false;
        public uint Length = 0;
        public string LengthField = "";
        public string Name = "";
        public string TypeName = "";
        public GeneratedField()
        {

        }
    }
    public class GeneratedDataClass
    {
        public GeneratedDataTypeDefinition? DataTypeDefinition = null;
        public string Name = "";
        public GeneratedDataClass()
        {
        }
    }
    public class GeneratedStructure : GeneratedDataClass
    {
        public string Documentation = "";
        public string? BaseType = null;
        public List<GeneratedField> fields = new List<GeneratedField>();
        public GeneratedStructure()
        {

        }
    }
    public class GeneratedEnumeratedType : GeneratedDataClass
    {
        public string Documentation = "";
        public GeneratedEnumeratedType()
        {

        }
    }
    public class GeneratedOpaqueType : GeneratedDataClass
    {
        public string Documentation = "";
        public GeneratedOpaqueType()
        {

        }
    }
    public enum GeneratedComplexTypes
    {
        StructuredType,
        EnumeratedType,
        OpaqueType
    }
}
