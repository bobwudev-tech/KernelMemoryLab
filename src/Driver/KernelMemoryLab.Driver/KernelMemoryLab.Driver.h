#pragma once

#include <ntifs.h>
#include <wdf.h>

#include "KernelMemoryLab.Protocol.h"

KML_OPERATION_STATUS
KmlValidateRequestHeaderFields(
    const KML_COMMON_REQUEST_HEADER* header);

KML_OPERATION_STATUS
KmlValidateRequestHeader(
    const KML_COMMON_REQUEST_HEADER* header,
    UINT32 expectedStructureSize);

NTSTATUS
KmlOperationStatusToNtStatus(
    KML_OPERATION_STATUS operationStatus);

VOID
KmlInitializeResponseHeader(
    KML_COMMON_RESPONSE_HEADER* header,
    KML_OPERATION_STATUS operationStatus,
    UINT32 bytesProcessed);

KML_OPERATION_STATUS
KmlValidateMemoryRange(
    UINT32 targetProcessId,
    UINT64 address,
    UINT32 size);

_IRQL_requires_(PASSIVE_LEVEL)
KML_OPERATION_STATUS
KmlAcquireAllowedTargetProcess(
    UINT32 targetProcessId,
    PEPROCESS* process);

_IRQL_requires_(PASSIVE_LEVEL)
KML_OPERATION_STATUS
KmlTransferTargetMemory(
    PEPROCESS process,
    UINT64 address,
    PVOID buffer,
    UINT32 size,
    BOOLEAN writeToTarget,
    UINT32* bytesTransferred,
    NTSTATUS* detailStatus);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleReadSingle(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleWriteSingle(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleReadBatch(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleWriteBatch(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength);
