# Kwy

Kwy is a modular .NET industrial application framework for building equipment software, automation systems, machine vision tools, and WPF-based engineering applications.

Kwy focuses on the reusable foundations that are repeatedly needed in industrial software projects: communication, device abstraction, MVVM, WPF UI, logging, files, licensing, vision algorithms, and ready-to-use project templates.

## Overview

Industrial equipment software usually contains much more than protocol read/write logic. A maintainable project also needs device lifecycle management, runtime state synchronization, safety checks, permissions, configuration editing, UI consistency, logging, file processing and deployment-friendly packaging.

Kwy provides these capabilities as separated, NuGet-friendly modules.

```text
Kwy.Communicate.*     Communication protocols and transport clients
Kwy.Device.*          Industrial device abstraction and runtime management
Kwy.UI.WPF.*          WPF themes, controls and reusable UI components
Kwy.MVVM.*            MVVM, modularity, region navigation and permissions
Kwy.Files.*           File, JSON, INI and Excel abstractions
Kwy.Logging.*         Logging abstractions and Serilog integration
Kwy.Licensing.*       License and dongle abstractions
Kwy.Vision.*          Vision abstractions, geometry models and algorithms
KwyTemplate.*         Application template for equipment software
```

## Features

- Communication modules
  - TCP / Serial
  - MQTT
  - OPC UA
  - Modbus based on FluentModbus
  - NI GPIB
  - SECS / GEM / GEM300 abstractions

- Device modules
  - PLC
  - IO card
  - Motion card
  - Camera
  - Instrument
  - Device registry by `DeviceId`
  - State synchronization, safety guard and recovery strategy foundations

- WPF UI modules
  - Light / Dark themes
  - Common control styles
  - Dialog service
  - Toast message service
  - Property grid generated from metadata
  - Flow designer foundation

- MVVM modules
  - Bindable base classes
  - Region navigation
  - Modular application structure
  - Dialog abstractions
  - Permission system
  - Message bus based on CommunityToolkit.Mvvm

- File modules
  - JSON helper
  - INI support
  - Excel abstractions
  - EPPlus / NPOI / Interop implementations

- Vision modules
  - Vision abstractions
  - Geometry models
  - HALCON integration
  - OpenCV extension direction
  - Measurement, calibration, code reading and preprocessing foundations

- Template projects
  - Shell
  - App
  - Device
  - Flow
  - Security
  - Vision

## Architecture

Kwy is designed around high cohesion and low coupling. Each module owns a clear responsibility and can be referenced independently.

```text
Application
  KwyTemplate.Shell
  KwyTemplate.App
  KwyTemplate.Flow
  KwyTemplate.Device
  KwyTemplate.Security

Framework
  Kwy.MVVM
  Kwy.MVVM.WPF
  Kwy.UI.WPF
  Kwy.UI.WPF.Components
  Kwy.Device.Abstractions
  Kwy.Device.Core
  Kwy.Communicate.Abstractions
  Kwy.Communicate.Core

Extensions
  Kwy.Device.PLCs.Hsl
  Kwy.Device.MotionCards.Googol
  Kwy.Device.MotionCards.Leadshine
  Kwy.Device.Cameras.HikVision
  Kwy.Communicate.Mqtt
  Kwy.Communicate.OpcUa
  Kwy.Communicate.FMdb
  Kwy.Vision.Halcon
```

## Device Connection Model

Template device connections are collection based. The main configuration model does not grow every time a new device type is added.

```json
{
  "devices": [
    {
      "deviceId": "PLC.Main",
      "deviceType": "HslPlc",
      "displayName": "Main PLC",
      "enabled": true,
      "connectOnStartup": true,
      "config": {}
    }
  ]
}
```

Each device type provides an `IDeviceConnectionFactory`.

```text
DeviceConnectionEntry
  -> DeviceType
  -> IDeviceConnectionFactory
  -> Strongly typed connection options
  -> Runtime device instance
  -> IDeviceRegistry
```

This keeps the template stable while allowing customer projects to add different PLCs, cameras, motion cards or custom devices.

## Project Template

`KwyTemplate` is a practical starting point for industrial equipment software.

```text
KwyTemplate.Shell
  Main window, title bar, status bar and module hosting.

KwyTemplate.App
  Main business UI, navigation and system configuration views.

KwyTemplate.Device
  Device connection configuration, persistence, factories and startup connection.

KwyTemplate.Flow
  Machine flow, device roles and business process orchestration.

KwyTemplate.Security
  Local user, role and permission management.

KwyTemplate.Vision
  Vision flow editor and image inspection UI foundation.
```

## Typical Use Cases

- Non-standard automation equipment
- Semiconductor equipment software
- PLC-based machine control software
- Vision inspection applications
- Industrial data acquisition tools
- WPF engineering software
- Equipment demo and project templates

## Getting Started

Clone the repository and build the solution:

```powershell
dotnet build Kwy.slnx
```

Run the template shell project:

```powershell
dotnet run --project KwyTemplate.Shell/KwyTemplate.Shell.csproj -f net8.0-windows
```

Pack NuGet packages locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1
```

Generated packages are placed under:

```text
artifacts/nuget
```

## NuGet Packaging Strategy

Kwy is intended to be consumed as modular NuGet packages.

Recommended usage:

```text
Install feature packages directly.
Foundation packages are usually resolved as dependencies.
```

Examples:

```text
Kwy.Communicate.FMdb
Kwy.Device.PLCs.Hsl
Kwy.UI.WPF.Components
Kwy.Vision.Halcon
```

Reference foundation packages directly only when developing custom drivers, protocols, authorization components or framework extensions.


## Packaging from Visual Studio

The repository includes a helper project named `Kwy.Packaging`.

Build this project in `Release` configuration to run the NuGet packaging script from Visual Studio:

```text
Right click Kwy.Packaging
  -> Build
  -> Configuration: Release
```

The packaging project calls:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1 -ChangedOnly -Configuration Release
```

The script detects changed projects from git, includes dependent packages, builds the solution once, and writes generated packages to:

```text
artifacts/nuget
```

By default, this local workflow only creates NuGet packages. It does not push packages to a remote NuGet source.

To preview affected packages without building or packing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Pack-KwyNuGet.ps1 -ChangedOnly -DryRun
```
## Design Principles

- Keep abstractions small and stable.
- Keep vendor-specific configuration inside vendor modules.
- Use strongly typed options instead of weak dictionaries where possible.
- Use `DeviceId` to distinguish runtime devices.
- Keep business flow outside the device connection layer.
- Prefer composition over inheritance for device capabilities.
- Keep UI theme resources replaceable.
- Make modules independently packageable.

## Status

Kwy is under active development.

The framework is currently focused on practical .NET / WPF industrial application development, with emphasis on maintainability, modularity and real equipment integration.

## License

License information will be added later.

