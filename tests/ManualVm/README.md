# Manual VM tests

Phase 02 includes a manual-only PING transport harness in `KernelMemoryLab.PingClient`.

Phase 04 includes a manual-only single user-memory transport harness in `KernelMemoryLab.SingleMemoryClient`.

Phase 05 includes a manual-only batch user-memory transport harness in `KernelMemoryLab.BatchMemoryClient`.

Phase 06 uses the real `KernelMemoryLab.Controller.exe` only during the user-run VM procedure.

Phase 07 packages the primitive Driver INF/SYS/CAT and documents manual VM installation and removal in `../../docs/Driver_Install.md`.

The Coding Agent may compile that project, but must never execute it because it opens the real device and calls `DeviceIoControl`.

Any future driver integration procedure placed here must begin with:

`MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`

See the phase checklists in this directory, including `Phase06_Controller_API_Checklist.md`, for user-run Windows 11 VM procedures.

