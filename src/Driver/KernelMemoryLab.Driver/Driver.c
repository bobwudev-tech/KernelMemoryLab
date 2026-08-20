#include <ntddk.h>
#include <wdf.h>
#include <wdmsec.h>

#include "KernelMemoryLab.Protocol.h"

#define KML_NT_DEVICE_NAME L"\\Device\\KernelMemoryLab"
#define KML_DOS_DEVICE_NAME L"\\DosDevices\\KernelMemoryLab"

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_UNLOAD KmlEvtDriverUnload;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL KmlEvtIoDeviceControl;

C_ASSERT(sizeof(KML_PROTOCOL_VERSION) == 4);
C_ASSERT(sizeof(KML_DRIVER_VERSION) == 8);
C_ASSERT(sizeof(KML_COMMON_REQUEST_HEADER) == 16);
C_ASSERT(sizeof(KML_COMMON_RESPONSE_HEADER) == 16);
C_ASSERT(sizeof(KML_GET_PROTOCOL_VERSION_REQUEST) == 16);
C_ASSERT(sizeof(KML_GET_PROTOCOL_VERSION_RESPONSE) == 16);
C_ASSERT(sizeof(KML_GET_CAPABILITIES_REQUEST) == 16);
C_ASSERT(sizeof(KML_GET_CAPABILITIES_RESPONSE) == 40);
C_ASSERT(sizeof(KML_PING_REQUEST) == 24);
C_ASSERT(sizeof(KML_PING_RESPONSE) == 40);
C_ASSERT(KML_MAX_BATCH_PAYLOAD_SIZE == 524288u);
C_ASSERT(KML_PHASE02_CAPABILITIES == 0x0000000000000007ull);
C_ASSERT(IOCTL_KML_GET_PROTOCOL_VERSION == 0x0022E000u);
C_ASSERT(IOCTL_KML_GET_CAPABILITIES == 0x0022E004u);
C_ASSERT(IOCTL_KML_PING == 0x0022E008u);
C_ASSERT(IOCTL_KML_READ_SINGLE == 0x0022E040u);
C_ASSERT(IOCTL_KML_WRITE_SINGLE == 0x0022E044u);
C_ASSERT(IOCTL_KML_READ_BATCH == 0x0022E048u);
C_ASSERT(IOCTL_KML_WRITE_BATCH == 0x0022E04Cu);

static NTSTATUS KmlCreateControlDevice(WDFDRIVER driver);

static KML_OPERATION_STATUS KmlValidateRequestHeader(
    const KML_COMMON_REQUEST_HEADER* header,
    UINT32 expectedStructureSize);

static NTSTATUS KmlOperationStatusToNtStatus(
    KML_OPERATION_STATUS operationStatus);

static VOID KmlInitializeResponseHeader(
    KML_COMMON_RESPONSE_HEADER* header,
    KML_OPERATION_STATUS operationStatus,
    UINT32 bytesProcessed);

static VOID KmlHandleGetProtocolVersion(
    WDFREQUEST request,
    size_t inputBufferLength);

static VOID KmlHandleGetCapabilities(
    WDFREQUEST request,
    size_t inputBufferLength);

static VOID KmlHandlePing(
    WDFREQUEST request,
    size_t inputBufferLength);

_Use_decl_annotations_
NTSTATUS
DriverEntry(
    PDRIVER_OBJECT driverObject,
    PUNICODE_STRING registryPath
)
{
    WDF_DRIVER_CONFIG config;
    WDFDRIVER driver;
    NTSTATUS status;

    WDF_DRIVER_CONFIG_INIT(&config, WDF_NO_EVENT_CALLBACK);
    config.DriverInitFlags |= WdfDriverInitNonPnpDriver;
    config.EvtDriverUnload = KmlEvtDriverUnload;

    status = WdfDriverCreate(
        driverObject,
        registryPath,
        WDF_NO_OBJECT_ATTRIBUTES,
        &config,
        &driver);

    if (!NT_SUCCESS(status)) {
        return status;
    }

    return KmlCreateControlDevice(driver);
}

_Use_decl_annotations_
VOID
KmlEvtDriverUnload(
    WDFDRIVER driver
)
{
    UNREFERENCED_PARAMETER(driver);
}

static NTSTATUS
KmlCreateControlDevice(
    WDFDRIVER driver
)
{
    DECLARE_CONST_UNICODE_STRING(deviceName, KML_NT_DEVICE_NAME);
    DECLARE_CONST_UNICODE_STRING(symbolicLinkName, KML_DOS_DEVICE_NAME);
    PWDFDEVICE_INIT deviceInit;
    WDFDEVICE device;
    WDF_IO_QUEUE_CONFIG queueConfig;
    NTSTATUS status;

    deviceInit = WdfControlDeviceInitAllocate(
        driver,
        &SDDL_DEVOBJ_SYS_ALL_ADM_ALL);

    if (deviceInit == NULL) {
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    WdfDeviceInitSetDeviceType(deviceInit, FILE_DEVICE_UNKNOWN);
    WdfDeviceInitSetCharacteristics(deviceInit, FILE_DEVICE_SECURE_OPEN, FALSE);
    WdfDeviceInitSetExclusive(deviceInit, FALSE);

    status = WdfDeviceInitAssignName(deviceInit, &deviceName);
    if (!NT_SUCCESS(status)) {
        WdfDeviceInitFree(deviceInit);
        return status;
    }

    status = WdfDeviceCreate(
        &deviceInit,
        WDF_NO_OBJECT_ATTRIBUTES,
        &device);

    if (!NT_SUCCESS(status)) {
        if (deviceInit != NULL) {
            WdfDeviceInitFree(deviceInit);
        }

        return status;
    }

    status = WdfDeviceCreateSymbolicLink(device, &symbolicLinkName);
    if (!NT_SUCCESS(status)) {
        WdfObjectDelete(device);
        return status;
    }

    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(
        &queueConfig,
        WdfIoQueueDispatchParallel);
    queueConfig.EvtIoDeviceControl = KmlEvtIoDeviceControl;

    status = WdfIoQueueCreate(
        device,
        &queueConfig,
        WDF_NO_OBJECT_ATTRIBUTES,
        WDF_NO_HANDLE);

    if (!NT_SUCCESS(status)) {
        WdfObjectDelete(device);
        return status;
    }

    WdfControlFinishInitializing(device);
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
VOID
KmlEvtIoDeviceControl(
    WDFQUEUE queue,
    WDFREQUEST request,
    size_t outputBufferLength,
    size_t inputBufferLength,
    ULONG ioControlCode
)
{
    UNREFERENCED_PARAMETER(queue);
    UNREFERENCED_PARAMETER(outputBufferLength);

    switch (ioControlCode) {
    case IOCTL_KML_GET_PROTOCOL_VERSION:
        KmlHandleGetProtocolVersion(request, inputBufferLength);
        break;

    case IOCTL_KML_GET_CAPABILITIES:
        KmlHandleGetCapabilities(request, inputBufferLength);
        break;

    case IOCTL_KML_PING:
        KmlHandlePing(request, inputBufferLength);
        break;

    case IOCTL_KML_READ_SINGLE:
    case IOCTL_KML_WRITE_SINGLE:
    case IOCTL_KML_READ_BATCH:
    case IOCTL_KML_WRITE_BATCH:
        WdfRequestComplete(request, STATUS_NOT_SUPPORTED);
        break;

    default:
        WdfRequestComplete(request, STATUS_INVALID_DEVICE_REQUEST);
        break;
    }
}

static KML_OPERATION_STATUS
KmlValidateRequestHeader(
    const KML_COMMON_REQUEST_HEADER* header,
    UINT32 expectedStructureSize
)
{
    if ((header->ProtocolVersion.Major != KML_PROTOCOL_VERSION_MAJOR) ||
        (header->ProtocolVersion.Minor != KML_PROTOCOL_VERSION_MINOR)) {
        return KmlOperationUnsupportedProtocolVersion;
    }

    if (header->StructureSize != expectedStructureSize) {
        return KmlOperationInvalidStructureSize;
    }

    if (header->Flags != 0u) {
        return KmlOperationInvalidFlags;
    }

    if (header->Reserved != 0u) {
        return KmlOperationInvalidReservedField;
    }

    return KmlOperationSuccess;
}

static NTSTATUS
KmlOperationStatusToNtStatus(
    KML_OPERATION_STATUS operationStatus
)
{
    switch (operationStatus) {
    case KmlOperationSuccess:
        return STATUS_SUCCESS;
    case KmlOperationUnsupportedProtocolVersion:
        return STATUS_REVISION_MISMATCH;
    case KmlOperationInvalidStructureSize:
        return STATUS_INFO_LENGTH_MISMATCH;
    case KmlOperationInvalidFlags:
    case KmlOperationInvalidReservedField:
        return STATUS_INVALID_PARAMETER;
    case KmlOperationUnsupportedOperation:
        return STATUS_NOT_SUPPORTED;
    case KmlOperationBufferTooSmall:
        return STATUS_BUFFER_TOO_SMALL;
    default:
        return STATUS_INTERNAL_ERROR;
    }
}

static VOID
KmlInitializeResponseHeader(
    KML_COMMON_RESPONSE_HEADER* header,
    KML_OPERATION_STATUS operationStatus,
    UINT32 bytesProcessed
)
{
    header->ProtocolVersion.Major = KML_PROTOCOL_VERSION_MAJOR;
    header->ProtocolVersion.Minor = KML_PROTOCOL_VERSION_MINOR;
    header->OperationStatus = (UINT32)operationStatus;
    header->BytesProcessed = bytesProcessed;
    header->DetailStatus = (UINT32)KmlOperationStatusToNtStatus(operationStatus);
}

static VOID
KmlHandleGetProtocolVersion(
    WDFREQUEST request,
    size_t inputBufferLength
)
{
    PKML_GET_PROTOCOL_VERSION_REQUEST input;
    PKML_GET_PROTOCOL_VERSION_RESPONSE output;
    KML_COMMON_REQUEST_HEADER requestHeader;
    KML_OPERATION_STATUS operationStatus;
    NTSTATUS status;

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_COMMON_REQUEST_HEADER),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(&requestHeader, &input->Header, sizeof(requestHeader));

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_GET_PROTOCOL_VERSION_RESPONSE),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(output, sizeof(*output));

    operationStatus = (inputBufferLength == sizeof(KML_GET_PROTOCOL_VERSION_REQUEST))
        ? KmlValidateRequestHeader(
            &requestHeader,
            (UINT32)sizeof(KML_GET_PROTOCOL_VERSION_REQUEST))
        : KmlOperationInvalidStructureSize;

    KmlInitializeResponseHeader(&output->Header, operationStatus, 0u);
    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        sizeof(*output));
}

static VOID
KmlHandleGetCapabilities(
    WDFREQUEST request,
    size_t inputBufferLength
)
{
    PKML_GET_CAPABILITIES_REQUEST input;
    PKML_GET_CAPABILITIES_RESPONSE output;
    KML_COMMON_REQUEST_HEADER requestHeader;
    KML_OPERATION_STATUS operationStatus;
    NTSTATUS status;

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_COMMON_REQUEST_HEADER),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(&requestHeader, &input->Header, sizeof(requestHeader));

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_GET_CAPABILITIES_RESPONSE),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(output, sizeof(*output));

    operationStatus = (inputBufferLength == sizeof(KML_GET_CAPABILITIES_REQUEST))
        ? KmlValidateRequestHeader(
            &requestHeader,
            (UINT32)sizeof(KML_GET_CAPABILITIES_REQUEST))
        : KmlOperationInvalidStructureSize;

    KmlInitializeResponseHeader(&output->Header, operationStatus, 0u);

    if (operationStatus == KmlOperationSuccess) {
        output->Capabilities = KML_PHASE02_CAPABILITIES;
        output->MaxSingleItemSize = KML_MAX_SINGLE_ITEM_SIZE;
        output->MaxBatchItems = KML_MAX_BATCH_ITEMS;
        output->MaxBatchPayloadSize = KML_MAX_BATCH_PAYLOAD_SIZE;
        output->Reserved = 0u;
    }

    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        sizeof(*output));
}

static VOID
KmlHandlePing(
    WDFREQUEST request,
    size_t inputBufferLength
)
{
    PKML_PING_REQUEST input;
    PKML_PING_RESPONSE output;
    KML_PING_REQUEST pingRequest;
    KML_OPERATION_STATUS operationStatus;
    NTSTATUS status;

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_COMMON_REQUEST_HEADER),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(&pingRequest, sizeof(pingRequest));
    RtlCopyMemory(
        &pingRequest.Header,
        &input->Header,
        sizeof(pingRequest.Header));

    if (inputBufferLength == sizeof(KML_PING_REQUEST)) {
        RtlCopyMemory(&pingRequest, input, sizeof(pingRequest));
    }

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_PING_RESPONSE),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(output, sizeof(*output));

    operationStatus = (inputBufferLength == sizeof(KML_PING_REQUEST))
        ? KmlValidateRequestHeader(
            &pingRequest.Header,
            (UINT32)sizeof(KML_PING_REQUEST))
        : KmlOperationInvalidStructureSize;

    KmlInitializeResponseHeader(
        &output->Header,
        operationStatus,
        (operationStatus == KmlOperationSuccess) ? (UINT32)sizeof(UINT64) : 0u);

    if (operationStatus == KmlOperationSuccess) {
        output->DriverVersion.Major = KML_DRIVER_VERSION_MAJOR;
        output->DriverVersion.Minor = KML_DRIVER_VERSION_MINOR;
        output->DriverVersion.Build = KML_DRIVER_VERSION_BUILD;
        output->DriverVersion.Revision = KML_DRIVER_VERSION_REVISION;
        output->Capabilities = KML_PHASE02_CAPABILITIES;
        output->EchoToken = pingRequest.Token;
    }

    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        sizeof(*output));
}
