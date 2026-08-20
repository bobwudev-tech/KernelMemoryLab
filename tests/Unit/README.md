# Unit tests

`KernelMemoryLab.Protocol.Tests` is a dependency-free, pure user-mode test executable for protocol structure sizes, IOCTL constants, limits, and serialization.

`KernelMemoryLab.Target.Tests` is a dependency-free, pure user-mode test executable for the Target unmanaged layout, initial values, self-memory reads/writes, non-cached UI refresh behavior, disposal, and address stability.

Run it only as a user-mode test:

```powershell
dotnet run --project .\tests\Unit\KernelMemoryLab.Protocol.Tests\KernelMemoryLab.Protocol.Tests.csproj -c Debug -p:Platform=x64
dotnet run --project .\tests\Unit\KernelMemoryLab.Target.Tests\KernelMemoryLab.Target.Tests.csproj -c Debug -p:Platform=x64
```

Tests in this directory must never communicate with a real kernel driver.

