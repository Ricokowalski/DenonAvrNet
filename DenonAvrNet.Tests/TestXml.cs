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

    internal const string AppCommandMainZoneStatus = """
        <?xml version="1.0" encoding="utf-8"?>
        <rx>
          <cmd><zone1>ON</zone1><zone2>OFF</zone2><zone3>OFF</zone3></cmd>
          <cmd>
            <zone1><volume>-35.5</volume><state>variable</state></zone1>
            <zone2><volume>-40</volume></zone2>
            <zone3><volume>-40</volume></zone3>
          </cmd>
          <cmd><zone1>off</zone1><zone2>off</zone2><zone3>off</zone3></cmd>
          <cmd>
            <zone1><source>MPLAY</source></zone1>
            <zone2><source>SOURCE</source></zone2>
            <zone3><source>SOURCE</source></zone3>
          </cmd>
          <cmd>
            <functiondelete>
              <list><name>CBL/SAT</name><FuncName>CBL/SAT</FuncName><use>1</use></list>
              <list><name>Media Player</name><FuncName>Media Player</FuncName><use>1</use></list>
              <list><name>TV AUDIO</name><FuncName>TV AUDIO</FuncName><use>0</use></list>
              <list><name>PHONO</name><FuncName>PHONO</FuncName><use>1</use></list>
            </functiondelete>
          </cmd>
        </rx>
        """;
}
