#include "KernelMemoryLab.Driver.h"

_IRQL_requires_(PASSIVE_LEVEL)
static BOOLEAN
KmlProcessImageIsAllowed(
    PEPROCESS process);

static VOID
KmlCompleteSingleRequest(
    WDFREQUEST request,
    KML_COMMON_RESPONSE_HEADER* response,
    KML_OPERATION_STATUS operationStatus,
    UINT32 bytesProcessed,
    NTSTATUS detailStatus,
    size_t responseSize);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleReadSingle(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength)
{
    PKML_READ_SINGLE_REQUEST input;
    PKML_READ_SINGLE_RESPONSE output;
    KML_READ_SINGLE_REQUEST localRequest;
    KML_OPERATION_STATUS operationStatus;
    PEPROCESS process;
    UINT32 bytesTransferred;
    NTSTATUS detailStatus;
    NTSTATUS status;
    size_t requiredOutputSize;
    size_t responseSize;

    process = NULL;
    bytesTransferred = 0u;
    detailStatus = STATUS_SUCCESS;
    responseSize = sizeof(KML_COMMON_RESPONSE_HEADER);

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_READ_SINGLE_REQUEST),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(&localRequest, input, sizeof(localRequest));

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_COMMON_RESPONSE_HEADER),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(output, sizeof(KML_COMMON_RESPONSE_HEADER));

    operationStatus = KmlValidateRequestHeader(
        &localRequest.Header,
        (UINT32)sizeof(KML_READ_SINGLE_REQUEST));

    if ((operationStatus == KmlOperationSuccess) &&
        (inputBufferLength != sizeof(KML_READ_SINGLE_REQUEST))) {
        operationStatus = KmlOperationInvalidRequest;
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlValidateMemoryRange(
            localRequest.TargetProcessId,
            localRequest.Address,
            localRequest.Size);
    }

    if (operationStatus == KmlOperationSuccess) {
        requiredOutputSize =
            sizeof(KML_COMMON_RESPONSE_HEADER) + (size_t)localRequest.Size;

        if (outputBufferLength < requiredOutputSize) {
            operationStatus = KmlOperationBufferTooSmall;
        } else {
            RtlZeroMemory(output, requiredOutputSize);
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlAcquireAllowedTargetProcess(
            localRequest.TargetProcessId,
            &process);
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlTransferTargetMemory(
            process,
            localRequest.Address,
            output->Data,
            localRequest.Size,
            FALSE,
            &bytesTransferred,
            &detailStatus);
    }

    if (process != NULL) {
        ObDereferenceObject(process);
    }

    if ((operationStatus == KmlOperationSuccess) ||
        (operationStatus == KmlOperationPartialTransfer)) {
        responseSize += bytesTransferred;
    }

    KmlCompleteSingleRequest(
        request,
        &output->Header,
        operationStatus,
        bytesTransferred,
        detailStatus,
        responseSize);
}

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleWriteSingle(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength)
{
    PKML_WRITE_SINGLE_REQUEST input;
    PKML_WRITE_SINGLE_RESPONSE output;
    KML_WRITE_SINGLE_REQUEST localRequest;
    KML_OPERATION_STATUS operationStatus;
    PEPROCESS process;
    UINT32 bytesTransferred;
    NTSTATUS detailStatus;
    NTSTATUS status;
    size_t expectedInputSize;

    UNREFERENCED_PARAMETER(outputBufferLength);

    process = NULL;
    bytesTransferred = 0u;
    detailStatus = STATUS_SUCCESS;

    status = WdfRequestRetrieveInputBuffer(
        request,
        KML_WRITE_SINGLE_REQUEST_HEADER_SIZE,
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(
        &localRequest,
        input,
        KML_WRITE_SINGLE_REQUEST_HEADER_SIZE);

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_WRITE_SINGLE_RESPONSE),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlZeroMemory(output, sizeof(*output));

    operationStatus = KmlValidateRequestHeaderFields(&localRequest.Header);

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlValidateMemoryRange(
            localRequest.TargetProcessId,
            localRequest.Address,
            localRequest.Size);
    }

    if (operationStatus == KmlOperationSuccess) {
        expectedInputSize =
            KML_WRITE_SINGLE_REQUEST_HEADER_SIZE + (size_t)localRequest.Size;

        if (localRequest.Header.StructureSize != expectedInputSize) {
            operationStatus = KmlOperationInvalidStructureSize;
        } else if (inputBufferLength != expectedInputSize) {
            operationStatus = KmlOperationInvalidRequest;
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlAcquireAllowedTargetProcess(
            localRequest.TargetProcessId,
            &process);
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlTransferTargetMemory(
            process,
            localRequest.Address,
            input->Data,
            localRequest.Size,
            TRUE,
            &bytesTransferred,
            &detailStatus);
    }

    if (process != NULL) {
        ObDereferenceObject(process);
    }

    KmlCompleteSingleRequest(
        request,
        &output->Header,
        operationStatus,
        bytesTransferred,
        detailStatus,
        sizeof(*output));
}

KML_OPERATION_STATUS
KmlValidateMemoryRange(
    UINT32 targetProcessId,
    UINT64 address,
    UINT32 size)
{
    UINT64 endExclusive;
    UINT64 lastAddress;
    UINT64 highestUserAddress;
    UINT64 systemRangeStart;

    if (targetProcessId == 0u) {
        return KmlOperationInvalidPid;
    }

    if (address == 0ull) {
        return KmlOperationInvalidAddress;
    }

    if ((size == 0u) || (size > KML_MAX_SINGLE_ITEM_SIZE)) {
        return KmlOperationInvalidSize;
    }

    if (address > (MAXUINT64 - (UINT64)size)) {
        return KmlOperationAddressRangeOverflow;
    }

    endExclusive = address + (UINT64)size;
    lastAddress = endExclusive - 1ull;
    highestUserAddress = (UINT64)(ULONG_PTR)MmHighestUserAddress;
    systemRangeStart = (UINT64)(ULONG_PTR)MmSystemRangeStart;

    if ((address >= systemRangeStart) || (lastAddress >= systemRangeStart)) {
        return KmlOperationKernelRangeDenied;
    }

    if ((address > highestUserAddress) || (lastAddress > highestUserAddress)) {
        return KmlOperationInvalidAddress;
    }

    return KmlOperationSuccess;
}

_IRQL_requires_(PASSIVE_LEVEL)
KML_OPERATION_STATUS
KmlAcquireAllowedTargetProcess(
    UINT32 targetProcessId,
    PEPROCESS* process)
{
    PEPROCESS candidate;
    NTSTATUS status;

    *process = NULL;

    status = PsLookupProcessByProcessId(
        ULongToHandle(targetProcessId),
        &candidate);

    if (!NT_SUCCESS(status)) {
        return ((status == STATUS_INVALID_CID) ||
                (status == STATUS_INVALID_PARAMETER))
            ? KmlOperationTargetNotFound
            : KmlOperationInternalError;
    }

    if (candidate == PsInitialSystemProcess) {
        ObDereferenceObject(candidate);
        return KmlOperationInvalidPid;
    }

    if (PsGetProcessExitStatus(candidate) != STATUS_PENDING) {
        ObDereferenceObject(candidate);
        return KmlOperationTargetExited;
    }

    if (!KmlProcessImageIsAllowed(candidate)) {
        KML_OPERATION_STATUS operationStatus;

        operationStatus =
            (PsGetProcessExitStatus(candidate) == STATUS_PENDING)
                ? KmlOperationTargetNotAllowed
                : KmlOperationTargetExited;

        ObDereferenceObject(candidate);
        return operationStatus;
    }

    *process = candidate;
    return KmlOperationSuccess;
}

_IRQL_requires_(PASSIVE_LEVEL)
static BOOLEAN
KmlProcessImageIsAllowed(
    PEPROCESS process)
{
    DECLARE_CONST_UNICODE_STRING(
        expectedImageName,
        L"KernelMemoryLab.Target.exe");
    PUNICODE_STRING imagePath;
    UNICODE_STRING baseName;
    USHORT characterCount;
    USHORT index;
    NTSTATUS status;
    BOOLEAN allowed;

    imagePath = NULL;
    status = SeLocateProcessImageName(process, &imagePath);

    if (!NT_SUCCESS(status) || (imagePath == NULL)) {
        if (imagePath != NULL) {
            ExFreePool(imagePath);
        }

        return FALSE;
    }

    baseName = *imagePath;
    characterCount = imagePath->Length / sizeof(WCHAR);

    for (index = characterCount; index > 0u; --index) {
        if ((imagePath->Buffer[index - 1u] == L'\\') ||
            (imagePath->Buffer[index - 1u] == L'/')) {
            baseName.Buffer = &imagePath->Buffer[index];
            baseName.Length =
                (USHORT)(imagePath->Length - (index * sizeof(WCHAR)));
            baseName.MaximumLength = baseName.Length;
            break;
        }
    }

    allowed = RtlEqualUnicodeString(
        &baseName,
        &expectedImageName,
        TRUE);

    ExFreePool(imagePath);
    return allowed;
}

_IRQL_requires_(PASSIVE_LEVEL)
KML_OPERATION_STATUS
KmlTransferTargetMemory(
    PEPROCESS process,
    UINT64 address,
    PVOID buffer,
    UINT32 size,
    BOOLEAN writeToTarget,
    UINT32* bytesTransferred,
    NTSTATUS* detailStatus)
{
    KAPC_STATE apcState;
    PMDL memoryDescriptorList;
    PVOID mappedAddress;
    KML_OPERATION_STATUS operationStatus;
    BOOLEAN pagesLocked;

    *bytesTransferred = 0u;
    *detailStatus = STATUS_SUCCESS;
    pagesLocked = FALSE;

    if (PsGetProcessExitStatus(process) != STATUS_PENDING) {
        *detailStatus = STATUS_PROCESS_IS_TERMINATING;
        return KmlOperationTargetExited;
    }

    memoryDescriptorList = IoAllocateMdl(
        (PVOID)(ULONG_PTR)address,
        size,
        FALSE,
        FALSE,
        NULL);

    if (memoryDescriptorList == NULL) {
        *detailStatus = STATUS_INSUFFICIENT_RESOURCES;
        return KmlOperationInternalError;
    }

    operationStatus = KmlOperationSuccess;
    KeStackAttachProcess((PRKPROCESS)process, &apcState);

    if (PsGetProcessExitStatus(process) != STATUS_PENDING) {
        operationStatus = KmlOperationTargetExited;
        *detailStatus = STATUS_PROCESS_IS_TERMINATING;
    } else {
        __try {
            MmProbeAndLockPages(
                memoryDescriptorList,
                UserMode,
                writeToTarget ? IoWriteAccess : IoReadAccess);
            pagesLocked = TRUE;
        }
        __except (EXCEPTION_EXECUTE_HANDLER) {
            operationStatus = KmlOperationMemoryNotAccessible;
            *detailStatus = GetExceptionCode();
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        mappedAddress = MmGetSystemAddressForMdlSafe(
            memoryDescriptorList,
            NormalPagePriority | MdlMappingNoExecute);

        if (mappedAddress == NULL) {
            operationStatus = KmlOperationMemoryNotAccessible;
            *detailStatus = STATUS_INSUFFICIENT_RESOURCES;
        } else {
            if (writeToTarget) {
                RtlCopyMemory(mappedAddress, buffer, size);
            } else {
                RtlCopyMemory(buffer, mappedAddress, size);
            }

            *bytesTransferred = size;
        }
    }

    if (pagesLocked) {
        MmUnlockPages(memoryDescriptorList);
    }

    KeUnstackDetachProcess(&apcState);
    IoFreeMdl(memoryDescriptorList);

    if ((operationStatus == KmlOperationMemoryNotAccessible) &&
        (PsGetProcessExitStatus(process) != STATUS_PENDING)) {
        operationStatus = KmlOperationTargetExited;
        *detailStatus = STATUS_PROCESS_IS_TERMINATING;
    }

    return operationStatus;
}

static VOID
KmlCompleteSingleRequest(
    WDFREQUEST request,
    KML_COMMON_RESPONSE_HEADER* response,
    KML_OPERATION_STATUS operationStatus,
    UINT32 bytesProcessed,
    NTSTATUS detailStatus,
    size_t responseSize)
{
    KmlInitializeResponseHeader(
        response,
        operationStatus,
        bytesProcessed);

    if (detailStatus != STATUS_SUCCESS) {
        response->DetailStatus = (UINT32)detailStatus;
    }

    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        responseSize);
}
