# Unit tests

`KernelMemoryLab.Protocol.Tests` is a dependency-free, pure user-mode test executable for protocol structure sizes, IOCTL constants, limits, and serialization.

Run it only as a user-mode test:

```powershell
dotnet run --project .\tests\Unit\KernelMemoryLab.Protocol.Tests\KernelMemoryLab.Protocol.Tests.csproj -c Debug -p:Platform=x64
```

Tests in this directory must never communicate with a real kernel driver.

