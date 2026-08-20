# Unit tests

`KernelMemoryLab.Protocol.Tests` is a dependency-free, pure user-mode test executable for protocol structure sizes, IOCTL constants, limits, serialization, single and batch request validation, malformed/overflowing batch offsets, item-count limits, and message encoding/decoding.

`KernelMemoryLab.Target.Tests` is a dependency-free, pure user-mode test executable for the Target unmanaged layout, initial values, self-memory reads/writes, non-cached UI refresh behavior, disposal, and address stability.

`KernelMemoryLab.Controller.Tests` uses only an in-memory fake `IDriverTransport`. It verifies API request serialization, typed helpers, single/batch behavior, pre-transport validation, structured error UX, and Driver-unavailable ViewModel behavior without opening a device.

Run it only as a user-mode test:

```powershell
dotnet run --project .\tests\Unit\KernelMemoryLab.Protocol.Tests\KernelMemoryLab.Protocol.Tests.csproj -c Debug -p:Platform=x64
dotnet run --project .\tests\Unit\KernelMemoryLab.Target.Tests\KernelMemoryLab.Target.Tests.csproj -c Debug -p:Platform=x64
dotnet run --project .\tests\Unit\KernelMemoryLab.Controller.Tests\KernelMemoryLab.Controller.Tests.csproj -c Debug -p:Platform=x64
```

Tests in this directory must never communicate with a real kernel driver.

