# Kwy.UI.WPF.FlowDesigner

`Kwy.UI.WPF.FlowDesigner` is an optional WPF flow-graph editor module for KwyFramework.
It provides node layout, port interaction, connection preview, snapping, routing, zoom, panning, and read-only preview. Business graph models and connection rules remain owned by the application.

## Registration

Merge exactly one theme dictionary in the consuming application's `App.xaml`:

```xml
<ResourceDictionary Source="pack://application:,,,/Kwy.UI.WPF.FlowDesigner;component/Themes/Light.xaml" />
```

Use `Themes/Dark.xaml` for the dark theme. These entry dictionaries merge the control styles and the selected module theme; applications that do not use the editor need not load them.

## Connection rules

Bind `KwyEditor.ConnectionValidator` to an application implementation of `IFlowConnectionValidator`. The editor supplies a `FlowConnectionRequest`; the application decides type compatibility, fan-in rules, circular-reference rules, and any domain restrictions.

`ConnectionCompletedCommand` remains the final write boundary and should validate again before persisting a graph change.

## License

Copyright © 2026 Kwy.

Licensed under the MIT License. See the repository licensing information for the applicable terms.
