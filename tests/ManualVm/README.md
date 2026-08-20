# Manual VM tests

Phase 02 includes a manual-only PING transport harness in `KernelMemoryLab.PingClient`.

Phase 04 includes a manual-only single user-memory transport harness in `KernelMemoryLab.SingleMemoryClient`.

The Coding Agent may compile that project, but must never execute it because it opens the real device and calls `DeviceIoControl`.

Any future driver integration procedure placed here must begin with:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

See `Phase02_Ping_Checklist.md` and `Phase04_Single_ReadWrite_Checklist.md` for the user-run Windows 11 VM procedures.

