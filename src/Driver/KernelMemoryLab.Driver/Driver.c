#include <ntddk.h>
#include <wdf.h>

DRIVER_INITIALIZE DriverEntry;

_Use_decl_annotations_
NTSTATUS
DriverEntry(
    PDRIVER_OBJECT driverObject,
    PUNICODE_STRING registryPath
)
{
    WDF_DRIVER_CONFIG config;

    WDF_DRIVER_CONFIG_INIT(&config, WDF_NO_EVENT_CALLBACK);

    // Phase 01 safety baseline: create only the framework driver object.
    // No device, queue, IOCTL, process access, or memory operation exists here.
    return WdfDriverCreate(
        driverObject,
        registryPath,
        WDF_NO_OBJECT_ATTRIBUTES,
        &config,
        WDF_NO_HANDLE);
}

