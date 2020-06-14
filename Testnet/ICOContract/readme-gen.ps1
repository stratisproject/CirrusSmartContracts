$sct = "C:\Users\ffres\Documents\GitHub\Stratis.SmartContracts.Tools.Sct\Stratis.SmartContracts.Tools.Sct\Stratis.SmartContracts.Tools.Sct.csproj"

$x = dotnet run -p $sct -- validate "ICOContract\ICOContract.cs" -sb | Out-String
#$x = & "C:\Program Files\cirrus-core-hackathon\resources\sdk\Stratis.SmartContracts.Tools.Sct.exe" validate "ICOContract\ICOContract.cs" -sb | Out-String


$hashIndex = $x.IndexOf("Hash") + 4
$byteIndex = $x.IndexOf("ByteCode") + 8
$end = $x.IndexOf("====")

("# ICO Smart Contract",
 "",
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