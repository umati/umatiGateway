# MQTT structure conversion regression tests

Run with the .NET 8 SDK/runtime:

```sh
dotnet test tests/unit/umatiGateway.Tests.csproj
```

The tests use independently written binary fixtures and SDK structure classes to check
nested job orders, optional fields, state arrays with RelativePath, already decoded
ExtensionObjects, Variant arrays, and truncated/trailing binary data. These are synthetic
fixtures; a live OPC UA server is still needed to validate loading its DataTypeDefinition
and publishing its actual JobOrderList through MQTT.
