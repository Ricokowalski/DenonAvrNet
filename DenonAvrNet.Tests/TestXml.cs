namespace DenonAvrNet.Tests;

internal static class TestXml
{
    internal const string DeviceInfo = """
        <?xml version="1.0" encoding="utf-8"?>
        <Device_Info>
          <DeviceInfoVers>0001</DeviceInfoVers>
          <CommApiVers>0301</CommApiVers>
          <CategoryName>AV RECEIVER</CategoryName>
          <ManualModelName>AVC-X6800H</ManualModelName>
          <ModelName>AVC-X6800H</ModelName>
          <MacAddress>001122334455</MacAddress>
          <DeviceZones>3</DeviceZones>
        </Device_Info>
        """;

    internal const string MainZoneStatus = """
        <?xml version="1.0" encoding="utf-8"?>
        <item>
          <Power><value>ON</value></Power>
          <InputFuncList>
            <value>CBL/SAT</value>
            <value>Media Player</value>
            <value>TV AUDIO</value>
            <value>PHONO</value>
          </InputFuncList>
          <InputFuncSelect><value>Media Player</value></InputFuncSelect>
          <MasterVolume><value>-35.5</value></MasterVolume>
          <Mute><value>off</value></Mute>
        </item>
        """;
}
