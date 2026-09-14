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

    internal const string AppCommandPower =
        "<rx><cmd><zone1>ON</zone1><zone2>OFF</zone2><zone3>OFF</zone3></cmd></rx>";

    internal const string AppCommandVolume = """
        <rx><cmd>
          <zone1><volume>-35.5</volume><state>variable</state></zone1>
          <zone2><volume>-40</volume></zone2>
          <zone3><volume>-40</volume></zone3>
        </cmd></rx>
        """;

    internal const string AppCommandMute =
        "<rx><cmd><zone1>off</zone1><zone2>off</zone2><zone3>off</zone3></cmd></rx>";

    internal const string AppCommandSource = """
        <rx><cmd>
          <zone1><source>MPLAY</source></zone1>
          <zone2><source>SOURCE</source></zone2>
          <zone3><source>SOURCE</source></zone3>
        </cmd></rx>
        """;

    internal const string AppCommandDeletedSources = """
        <rx><cmd><functiondelete>
          <list><name>CBL/SAT</name><FuncName>CBL/SAT</FuncName><use>1</use></list>
          <list><name>Media Player</name><FuncName>Media Player</FuncName><use>1</use></list>
          <list><name>TV AUDIO</name><FuncName>TV AUDIO</FuncName><use>0</use></list>
          <list><name>PHONO</name><FuncName>PHONO</FuncName><use>1</use></list>
        </functiondelete></cmd></rx>
        """;

    internal const string AppCommandError = "<rx><error>2</error></rx>";

    internal const string AppCommandAudioInfo = """
        <rx><cmd>
          <name>GetAudioInfo</name>
          <list>
            <param name="inputmode" control="1">HDMI</param>
            <param name="output" control="1">Speaker</param>
            <param name="signal" control="1">Dolby Audio - Dolby Digital Plus</param>
            <param name="sound" control="1">Dolby Surround</param>
            <param name="fs" control="1">48 kHz</param>
          </list>
        </cmd></rx>
        """;

    internal const string AppCommandActiveSpeakers = """
        <rx><cmd>
          <name>GetActiveSpeaker</name>
          <list>
            <param name="activespb1" control="2">SW</param>
            <param name="activespb2" control="2">FL</param>
            <param name="activespc2" control="1">C</param>
            <param name="activespd2" control="2">FR</param>
            <param name="activespb3" control="2">SL</param>
            <param name="activespd3" control="2">SR</param>
          </list>
        </cmd></rx>
        """;
}
