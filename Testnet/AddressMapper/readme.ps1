$sct = "C:\Users\ffres\Documents\GitHub\Stratis.SmartContracts.Tools.Sct\Stratis.SmartContracts.Tools.Sct\Stratis.SmartContracts.Tools.Sct.csproj"

$x = dotnet run -p $sct -- validate "AddressMapper\AddressMapper.cs" -sb | Out-String


$hashIndex = $x.IndexOf("Hash") + 4
$byteIndex = $x.IndexOf("ByteCode") + 8
$end = $x.IndexOf("====")

("# Address Mapper Contract",
 "",
  "**Compiler Version**",
 "``````" +
 "v0.0.2",
 "``````",
 "**Contract Hash**",
 "``````" +
 $x.Substring($hashIndex,66).Trim(),
 "``````",
 "",
 "**Contract Byte Code**",
 "``````",
 $x.Substring($byteIndex).replace("======","").Trim(),
 "``````"
 ) | Out-File -encoding utf8 README.MD 