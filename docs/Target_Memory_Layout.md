# KernelMemoryLab Target Memory Layout

`KernelMemoryLab.Target.exe` owns one 24-byte unmanaged allocation for its complete process lifetime. `App` allocates the block during startup and releases it during application exit. WPF bindings never use managed object addresses as test addresses.

## Layout

| Name | Type | Offset | Size | Initial value |
|---|---|---:|---:|---:|
| Health | Int32 | 0 | 4 | 100 |
| Mana | Int32 | 4 | 4 | 50 |
| Gold | Int64 | 8 | 8 | 1000 |
| PositionX | Float32 | 16 | 4 | 10.0 |
| PositionY | Float32 | 20 | 4 | 20.0 |

Each address is computed as `block base + fixed offset`. The base and variable addresses remain stable for one process lifetime; they may change after restarting the process.

## Refresh behavior

The window refreshes once per second. Every refresh calls `TargetMemoryBlock.ReadAll`, which reads each value directly from unmanaged memory before updating the bound row. It does not reuse the previous displayed value.

The UI displays the executable name, PID, block base address, variable name, type, address, current value, and last refresh time. No Driver is required to run the Target.
