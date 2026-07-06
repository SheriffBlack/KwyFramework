# Kwy.Communicate.FMdb

FluentModbus 5.3.2 client wrapper for Modbus TCP and Modbus RTU.

```csharp
var config = new FluentModbusConfig
{
    Transport = FluentModbusTransport.Tcp,
    Host = "192.168.1.10",
    Port = 502,
    UnitIdentifier = 1,
    ByteOrder = ModbusByteOrder.BigEndian
};

using var modbus = new FluentModbusCommunication(config);
await modbus.ConnectAsync();

var values = await modbus.ReadHoldingRegistersAsync<ushort>(0, 10);
await modbus.WriteSingleRegisterAsync(100, (ushort)1);
```

The generic register methods use FluentModbus data conversion. Use the raw methods when the device-specific register layout must be decoded manually.

## NuGet publish

`Kwy.Communicate.FMdb.csproj` is configured as a NuGet package example:

- `PackageId`: `Kwy.Communicate.FMdb`
- targets: `net8.0`, `net10.0`
- normal package: `.nupkg`
- symbol package: `.snupkg`
- XML documentation file included
- README included
- portable PDB generated
- source files embedded into PDB for step-in debugging

Create packages:

```powershell
dotnet pack Kwy.Communicate.FMdb\Kwy.Communicate.FMdb.csproj -c Release -o artifacts\nuget
```

Generated files:

```text
artifacts\nuget\Kwy.Communicate.FMdb.1.0.0.nupkg
artifacts\nuget\Kwy.Communicate.FMdb.1.0.0.snupkg
```

Publish to nuget.org:

```powershell
dotnet nuget push artifacts\nuget\Kwy.Communicate.FMdb.1.0.0.nupkg `
  --api-key <NUGET_API_KEY> `
  --source https://api.nuget.org/v3/index.json

dotnet nuget push artifacts\nuget\Kwy.Communicate.FMdb.1.0.0.snupkg `
  --api-key <NUGET_API_KEY> `
  --source https://api.nuget.org/v3/index.json
```

If the package should point to a fixed source repository, add an explicit `RepositoryUrl` in the project file. Otherwise the SDK may infer it from the current git remote.

## Debug into package source

The package emits a `.snupkg` symbol package and embeds source into the portable PDB. In Visual Studio, enable:

- `Tools` -> `Options` -> `Debugging` -> `Symbols` -> add `https://symbols.nuget.org/download/symbols`
- `Tools` -> `Options` -> `Debugging` -> enable Source Link support
- Disable `Just My Code` if Visual Studio still steps over NuGet package code

After the symbol package is available, consuming projects can set breakpoints or step into `Kwy.Communicate.FMdb` code from the NuGet package.
