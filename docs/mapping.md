# Mapping file

The mapping describes, per data-model element, which OPC UA nodes of a machine it is derived from and how.
The default is [`umatiGateway/mappings/machinetool-default.json`](../umatiGateway/mappings/machinetool-default.json);
a different file is selected with `MAPPING_FILE`. It is applied to **every** machine found in the Machines folder.

```json
{
  "modelVersion": "0.13.1",
  "profiles": ["Core", "IdentificationExtended", "Production", "Channel", "Spindle", "Energy"],
  "elements": [
    { "path": "State.FeedOverride",
      "inputs": { "v": "Monitoring/MachineTool/FeedOverride" },
      "transform": "scale", "params": { "factor": 0.01 },
      "unit": "one", "timestampSource": "source" }
  ]
}
```

| Field | Meaning |
|---|---|
| `path` | data-model path (`Group.Element`, `Consumption.<Resource>.<MeasuringPoint>.<Variable>`) |
| `inputs` | name → browse path relative to the machine object; browse names only, `/`-separated, namespace independent. `a/b\|c/d` = alternatives, the first that exists wins |
| `optionalInputs` | input names that may be missing without making the element unsupported |
| `transform` + `params` | `text`, `boolean`, `integer`, `number`, `scale` (`factor`, `offset`), `structSum` (sum of a structure's / array's fields), `enumObject` (several inputs → struct of enum names), `enumMap` (number → enum name) |
| `unit` | unit name from the catalog (`second`, `watt`, `wattHour`, `one`, ...). Sent with every number |
| `timestampSource` | `source` (OPC UA SourceTimestamp, default) or `adapter` (gateway time) |
| `correlationId` | the value carries the `ExecutionId` of the running program execution |
| `constant` | fixed value (e.g. `[]` for `Notification.ActiveAlerts`) |
| `special` | `heartbeat`, `connectionState`, `programExecution` (snapshot + events derived from the active program state; `params.stateMap` maps the OPC UA numbers) |
| `events` | event paths published by the element (listed in the manifest) |
| `instances` | `{group, under, pattern}`: element template with `<n>` in `path` and `inputs`, expanded once per child of `under` whose browse name matches the regex (group 1 = number). Fills `manifest.instances` |

An element is *supported* on a machine when all its non-optional inputs resolve; unsupported elements do not appear in that
machine's manifest, REST values or MQTT topics ("not supported = missing from the manifest").

To publish a different kind of machine, copy the default file, change the input paths and point `MAPPING_FILE` at it.
