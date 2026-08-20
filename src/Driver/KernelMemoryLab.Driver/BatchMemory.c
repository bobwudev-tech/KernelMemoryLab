#include "KernelMemoryLab.Driver.h"

#define KML_BATCH_POOL_TAG '5LMK'

typedef struct _KML_BATCH_ITEM_CONTEXT {
    UINT64 Address;
    UINT32 Size;
    UINT32 BufferOffset;
    KML_OPERATION_STATUS InitialStatus;
    KML_OPERATION_STATUS ResultStatus;
    UINT32 BytesProcessed;
    NTSTATUS DetailStatus;
} KML_BATCH_ITEM_CONTEXT, *PKML_BATCH_ITEM_CONTEXT;

static BOOLEAN
KmlBatchItemSizeIsValid(
    UINT32 size);

static KML_OPERATION_STATUS
KmlDetermineBatchStatus(
    UINT32 itemCount,
    UINT32 successfulItems,
    UINT32 bytesProcessed);

static VOID
KmlInitializeBatchItemResult(
    KML_BATCH_ITEM_RESULT* result,
    const KML_BATCH_ITEM_CONTEXT* context,
    UINT32 dataOffset);

static VOID
KmlCompleteBatchFailure(
    WDFREQUEST request,
    KML_COMMON_RESPONSE_HEADER* response,
    KML_OPERATION_STATUS operationStatus);

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleReadBatch(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength)
{
    PKML_READ_BATCH_REQUEST_HEADER input;
    PKML_READ_BATCH_RESPONSE_HEADER output;
    KML_READ_BATCH_REQUEST_HEADER localHeader;
    PKML_READ_BATCH_ITEM items;
    PKML_BATCH_ITEM_CONTEXT contexts;
    PKML_BATCH_ITEM_RESULT results;
    PEPROCESS process;
    KML_OPERATION_STATUS operationStatus;
    NTSTATUS status;
    UINT32 index;
    UINT32 aggregateSize;
    UINT32 itemsSize;
    UINT32 expectedInputSize;
    UINT32 resultsSize;
    UINT32 dataOffset;
    UINT32 requiredOutputSize;
    UINT32 totalBytesProcessed;
    UINT32 successfulItems;

    contexts = NULL;
    process = NULL;
    aggregateSize = 0u;
    itemsSize = 0u;
    expectedInputSize = 0u;
    resultsSize = 0u;
    dataOffset = 0u;
    requiredOutputSize = 0u;
    totalBytesProcessed = 0u;
    successfulItems = 0u;

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_READ_BATCH_REQUEST_HEADER),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(&localHeader, input, sizeof(localHeader));

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_COMMON_RESPONSE_HEADER),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    operationStatus = KmlValidateRequestHeaderFields(&localHeader.Header);

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.Reserved != 0u)) {
        operationStatus = KmlOperationInvalidReservedField;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.TargetProcessId == 0u)) {
        operationStatus = KmlOperationInvalidPid;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        ((localHeader.ItemCount == 0u) ||
         (localHeader.ItemCount > KML_MAX_BATCH_ITEMS))) {
        operationStatus = KmlOperationInvalidItemCount;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.ItemsOffset != sizeof(KML_READ_BATCH_REQUEST_HEADER))) {
        operationStatus = KmlOperationInvalidOffset;
    }

    if (operationStatus == KmlOperationSuccess) {
        if (localHeader.ItemCount >
            ((MAXUINT32 - localHeader.ItemsOffset) /
             (UINT32)sizeof(KML_READ_BATCH_ITEM))) {
            operationStatus = KmlOperationInvalidOffset;
        } else {
            itemsSize =
                localHeader.ItemCount * (UINT32)sizeof(KML_READ_BATCH_ITEM);
            expectedInputSize = localHeader.ItemsOffset + itemsSize;

            if (localHeader.Header.StructureSize != expectedInputSize) {
                operationStatus = KmlOperationInvalidStructureSize;
            } else if (inputBufferLength != expectedInputSize) {
                operationStatus = KmlOperationInvalidRequest;
            }
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        contexts = (PKML_BATCH_ITEM_CONTEXT)ExAllocatePool2(
            POOL_FLAG_NON_PAGED,
            sizeof(KML_BATCH_ITEM_CONTEXT) * KML_MAX_BATCH_ITEMS,
            KML_BATCH_POOL_TAG);

        if (contexts == NULL) {
            operationStatus = KmlOperationInternalError;
        } else {
            RtlZeroMemory(
                contexts,
                sizeof(KML_BATCH_ITEM_CONTEXT) * KML_MAX_BATCH_ITEMS);
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        items = (PKML_READ_BATCH_ITEM)(
            (PUCHAR)input + localHeader.ItemsOffset);

        for (index = 0u; index < localHeader.ItemCount; ++index) {
            contexts[index].Address = items[index].Address;
            contexts[index].Size = items[index].Size;
            contexts[index].BufferOffset = items[index].ResultOffset;
            contexts[index].InitialStatus = KmlValidateMemoryRange(
                localHeader.TargetProcessId,
                items[index].Address,
                items[index].Size);

            if (items[index].ResultOffset != aggregateSize) {
                operationStatus = KmlOperationInvalidOffset;
                break;
            }

            if (KmlBatchItemSizeIsValid(items[index].Size)) {
                if (aggregateSize >
                    (KML_MAX_BATCH_PAYLOAD_SIZE - items[index].Size)) {
                    operationStatus = KmlOperationAggregateLimitExceeded;
                    break;
                }

                aggregateSize += items[index].Size;
            }
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        resultsSize =
            localHeader.ItemCount * (UINT32)sizeof(KML_BATCH_ITEM_RESULT);
        dataOffset =
            (UINT32)sizeof(KML_READ_BATCH_RESPONSE_HEADER) + resultsSize;

        if (aggregateSize > (MAXUINT32 - dataOffset)) {
            operationStatus = KmlOperationAggregateLimitExceeded;
        } else {
            requiredOutputSize = dataOffset + aggregateSize;
            if (outputBufferLength < requiredOutputSize) {
                operationStatus = KmlOperationBufferTooSmall;
            }
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlAcquireAllowedTargetProcess(
            localHeader.TargetProcessId,
            &process);
    }

    if (operationStatus != KmlOperationSuccess) {
        if (contexts != NULL) {
            ExFreePoolWithTag(contexts, KML_BATCH_POOL_TAG);
        }

        KmlCompleteBatchFailure(
            request,
            &output->Header,
            operationStatus);
        return;
    }

    RtlZeroMemory(output, requiredOutputSize);
    output->ItemCount = localHeader.ItemCount;
    output->ResultsOffset = sizeof(KML_READ_BATCH_RESPONSE_HEADER);
    output->DataOffset = dataOffset;
    output->DataSize = aggregateSize;
    results = (PKML_BATCH_ITEM_RESULT)(
        (PUCHAR)output + output->ResultsOffset);

    for (index = 0u; index < localHeader.ItemCount; ++index) {
        contexts[index].ResultStatus = contexts[index].InitialStatus;
        contexts[index].DetailStatus = KmlOperationStatusToNtStatus(
            contexts[index].InitialStatus);

        if (contexts[index].InitialStatus == KmlOperationSuccess) {
            contexts[index].ResultStatus = KmlTransferTargetMemory(
                process,
                contexts[index].Address,
                (PUCHAR)output + dataOffset + contexts[index].BufferOffset,
                contexts[index].Size,
                FALSE,
                &contexts[index].BytesProcessed,
                &contexts[index].DetailStatus);
        }

        if (contexts[index].ResultStatus == KmlOperationSuccess) {
            ++successfulItems;
        }

        totalBytesProcessed += contexts[index].BytesProcessed;
        KmlInitializeBatchItemResult(
            &results[index],
            &contexts[index],
            dataOffset + contexts[index].BufferOffset);
    }

    operationStatus = KmlDetermineBatchStatus(
        localHeader.ItemCount,
        successfulItems,
        totalBytesProcessed);
    KmlInitializeResponseHeader(
        &output->Header,
        operationStatus,
        totalBytesProcessed);

    ObDereferenceObject(process);
    ExFreePoolWithTag(contexts, KML_BATCH_POOL_TAG);
    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        requiredOutputSize);
}

_IRQL_requires_(PASSIVE_LEVEL)
VOID
KmlHandleWriteBatch(
    WDFREQUEST request,
    size_t inputBufferLength,
    size_t outputBufferLength)
{
    PKML_WRITE_BATCH_REQUEST_HEADER input;
    PKML_WRITE_BATCH_RESPONSE_HEADER output;
    KML_WRITE_BATCH_REQUEST_HEADER localHeader;
    PKML_WRITE_BATCH_ITEM items;
    PKML_BATCH_ITEM_CONTEXT contexts;
    PKML_BATCH_ITEM_RESULT results;
    PEPROCESS process;
    KML_OPERATION_STATUS operationStatus;
    NTSTATUS status;
    UINT32 index;
    UINT32 aggregateSize;
    UINT32 itemsSize;
    UINT32 expectedDataOffset;
    UINT32 expectedInputSize;
    UINT32 requiredOutputSize;
    UINT32 totalBytesProcessed;
    UINT32 successfulItems;

    contexts = NULL;
    process = NULL;
    aggregateSize = 0u;
    itemsSize = 0u;
    expectedDataOffset = 0u;
    expectedInputSize = 0u;
    requiredOutputSize = 0u;
    totalBytesProcessed = 0u;
    successfulItems = 0u;

    status = WdfRequestRetrieveInputBuffer(
        request,
        sizeof(KML_WRITE_BATCH_REQUEST_HEADER),
        (PVOID*)&input,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    RtlCopyMemory(&localHeader, input, sizeof(localHeader));

    status = WdfRequestRetrieveOutputBuffer(
        request,
        sizeof(KML_COMMON_RESPONSE_HEADER),
        (PVOID*)&output,
        NULL);

    if (!NT_SUCCESS(status)) {
        WdfRequestComplete(request, status);
        return;
    }

    operationStatus = KmlValidateRequestHeaderFields(&localHeader.Header);

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.Reserved != 0u)) {
        operationStatus = KmlOperationInvalidReservedField;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.TargetProcessId == 0u)) {
        operationStatus = KmlOperationInvalidPid;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        ((localHeader.ItemCount == 0u) ||
         (localHeader.ItemCount > KML_MAX_BATCH_ITEMS))) {
        operationStatus = KmlOperationInvalidItemCount;
    }

    if ((operationStatus == KmlOperationSuccess) &&
        (localHeader.ItemsOffset != sizeof(KML_WRITE_BATCH_REQUEST_HEADER))) {
        operationStatus = KmlOperationInvalidOffset;
    }

    if (operationStatus == KmlOperationSuccess) {
        if (localHeader.ItemCount >
            ((MAXUINT32 - localHeader.ItemsOffset) /
             (UINT32)sizeof(KML_WRITE_BATCH_ITEM))) {
            operationStatus = KmlOperationInvalidOffset;
        } else {
            itemsSize =
                localHeader.ItemCount * (UINT32)sizeof(KML_WRITE_BATCH_ITEM);
            expectedDataOffset = localHeader.ItemsOffset + itemsSize;

            if (localHeader.DataOffset != expectedDataOffset) {
                operationStatus = KmlOperationInvalidOffset;
            } else if (localHeader.DataSize > KML_MAX_BATCH_PAYLOAD_SIZE) {
                operationStatus = KmlOperationAggregateLimitExceeded;
            } else if (localHeader.DataSize >
                       (MAXUINT32 - localHeader.DataOffset)) {
                operationStatus = KmlOperationInvalidOffset;
            } else {
                expectedInputSize = localHeader.DataOffset + localHeader.DataSize;

                if (localHeader.Header.StructureSize != expectedInputSize) {
                    operationStatus = KmlOperationInvalidStructureSize;
                } else if (inputBufferLength != expectedInputSize) {
                    operationStatus = KmlOperationInvalidRequest;
                }
            }
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        requiredOutputSize =
            (UINT32)sizeof(KML_WRITE_BATCH_RESPONSE_HEADER) +
            (localHeader.ItemCount * (UINT32)sizeof(KML_BATCH_ITEM_RESULT));

        if (outputBufferLength < requiredOutputSize) {
            operationStatus = KmlOperationBufferTooSmall;
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        contexts = (PKML_BATCH_ITEM_CONTEXT)ExAllocatePool2(
            POOL_FLAG_NON_PAGED,
            sizeof(KML_BATCH_ITEM_CONTEXT) * KML_MAX_BATCH_ITEMS,
            KML_BATCH_POOL_TAG);

        if (contexts == NULL) {
            operationStatus = KmlOperationInternalError;
        } else {
            RtlZeroMemory(
                contexts,
                sizeof(KML_BATCH_ITEM_CONTEXT) * KML_MAX_BATCH_ITEMS);
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        items = (PKML_WRITE_BATCH_ITEM)(
            (PUCHAR)input + localHeader.ItemsOffset);

        for (index = 0u; index < localHeader.ItemCount; ++index) {
            contexts[index].Address = items[index].Address;
            contexts[index].Size = items[index].Size;
            contexts[index].BufferOffset = items[index].DataOffset;
            contexts[index].InitialStatus = KmlValidateMemoryRange(
                localHeader.TargetProcessId,
                items[index].Address,
                items[index].Size);

            if ((aggregateSize > (MAXUINT32 - localHeader.DataOffset)) ||
                (items[index].DataOffset !=
                 (localHeader.DataOffset + aggregateSize))) {
                operationStatus = KmlOperationInvalidOffset;
                break;
            }

            if (KmlBatchItemSizeIsValid(items[index].Size)) {
                if (aggregateSize >
                    (KML_MAX_BATCH_PAYLOAD_SIZE - items[index].Size)) {
                    operationStatus = KmlOperationAggregateLimitExceeded;
                    break;
                }

                aggregateSize += items[index].Size;
            }
        }

        if ((operationStatus == KmlOperationSuccess) &&
            (aggregateSize != localHeader.DataSize)) {
            operationStatus = KmlOperationInvalidRequest;
        }
    }

    if (operationStatus == KmlOperationSuccess) {
        operationStatus = KmlAcquireAllowedTargetProcess(
            localHeader.TargetProcessId,
            &process);
    }

    if (operationStatus != KmlOperationSuccess) {
        if (contexts != NULL) {
            ExFreePoolWithTag(contexts, KML_BATCH_POOL_TAG);
        }

        KmlCompleteBatchFailure(
            request,
            &output->Header,
            operationStatus);
        return;
    }

    for (index = 0u; index < localHeader.ItemCount; ++index) {
        contexts[index].ResultStatus = contexts[index].InitialStatus;
        contexts[index].DetailStatus = KmlOperationStatusToNtStatus(
            contexts[index].InitialStatus);

        if (contexts[index].InitialStatus == KmlOperationSuccess) {
            contexts[index].ResultStatus = KmlTransferTargetMemory(
                process,
                contexts[index].Address,
                (PUCHAR)input + contexts[index].BufferOffset,
                contexts[index].Size,
                TRUE,
                &contexts[index].BytesProcessed,
                &contexts[index].DetailStatus);
        }

        if (contexts[index].ResultStatus == KmlOperationSuccess) {
            ++successfulItems;
        }

        totalBytesProcessed += contexts[index].BytesProcessed;
    }

    operationStatus = KmlDetermineBatchStatus(
        localHeader.ItemCount,
        successfulItems,
        totalBytesProcessed);

    RtlZeroMemory(output, requiredOutputSize);
    output->ItemCount = localHeader.ItemCount;
    output->ResultsOffset = sizeof(KML_WRITE_BATCH_RESPONSE_HEADER);
    results = (PKML_BATCH_ITEM_RESULT)(
        (PUCHAR)output + output->ResultsOffset);

    for (index = 0u; index < localHeader.ItemCount; ++index) {
        KmlInitializeBatchItemResult(
            &results[index],
            &contexts[index],
            0u);
    }

    KmlInitializeResponseHeader(
        &output->Header,
        operationStatus,
        totalBytesProcessed);

    ObDereferenceObject(process);
    ExFreePoolWithTag(contexts, KML_BATCH_POOL_TAG);
    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        requiredOutputSize);
}

static BOOLEAN
KmlBatchItemSizeIsValid(
    UINT32 size)
{
    return (size > 0u) && (size <= KML_MAX_SINGLE_ITEM_SIZE);
}

static KML_OPERATION_STATUS
KmlDetermineBatchStatus(
    UINT32 itemCount,
    UINT32 successfulItems,
    UINT32 bytesProcessed)
{
    if (successfulItems == itemCount) {
        return KmlOperationSuccess;
    }

    if (bytesProcessed > 0u) {
        return KmlOperationPartialTransfer;
    }

    return KmlOperationAllItemsFailed;
}

static VOID
KmlInitializeBatchItemResult(
    KML_BATCH_ITEM_RESULT* result,
    const KML_BATCH_ITEM_CONTEXT* context,
    UINT32 dataOffset)
{
    result->OperationStatus = (UINT32)context->ResultStatus;
    result->BytesProcessed = context->BytesProcessed;
    result->DataOffset = dataOffset;
    result->RequestedSize = context->Size;
    result->DetailStatus = (UINT32)context->DetailStatus;
    result->Reserved = 0u;
}

static VOID
KmlCompleteBatchFailure(
    WDFREQUEST request,
    KML_COMMON_RESPONSE_HEADER* response,
    KML_OPERATION_STATUS operationStatus)
{
    RtlZeroMemory(response, sizeof(*response));
    KmlInitializeResponseHeader(response, operationStatus, 0u);
    WdfRequestCompleteWithInformation(
        request,
        STATUS_SUCCESS,
        sizeof(*response));
}
