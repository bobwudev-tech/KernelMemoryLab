using System.Runtime.InteropServices;

namespace KernelMemoryLab.Protocol;

public readonly record struct BatchReadRequestItem(ulong Address, uint Size);

public sealed record BatchWriteRequestItem(ulong Address, ReadOnlyMemory<byte> Data);

public sealed record BatchReadResponseMessage(
    ReadBatchResponseHeader Header,
    IReadOnlyList<BatchItemResult> Results,
    ReadOnlyMemory<byte> Data);

public sealed record BatchWriteResponseMessage(
    WriteBatchResponseHeader Header,
    IReadOnlyList<BatchItemResult> Results);

public static class BatchMemoryProtocol
{
    public const int ReadRequestHeaderSize = 32;
    public const int WriteRequestHeaderSize = 40;
    public const int ItemSize = 16;
    public const int ReadResponseHeaderSize = 32;
    public const int WriteResponseHeaderSize = 24;
    public const int ItemResultSize = 24;

    public static byte[] EncodeReadRequest(
        uint targetProcessId,
        IReadOnlyList<BatchReadRequestItem> items)
    {
        ValidateEncoderItemCount(items);

        uint resultOffset = 0;
        ReadBatchItem[] wireItems = new ReadBatchItem[items.Count];
        for (int index = 0; index < items.Count; index++)
        {
            BatchReadRequestItem item = items[index];
            ValidateEncoderItemSize(item.Size, nameof(items));

            wireItems[index] = new ReadBatchItem
            {
                Address = item.Address,
                Size = item.Size,
                ResultOffset = resultOffset,
            };

            resultOffset = checked(resultOffset + item.Size);
            if (resultOffset > ProtocolConstants.MaxBatchPayloadSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(items),
                    "Aggregate read payload exceeds the protocol limit.");
            }
        }

        int totalSize = checked(ReadRequestHeaderSize + checked(ItemSize * items.Count));
        ReadBatchRequestHeader header = new()
        {
            Header = CreateCommonRequestHeader(totalSize),
            TargetProcessId = targetProcessId,
            ItemCount = checked((uint)items.Count),
            ItemsOffset = ReadRequestHeaderSize,
            Reserved = 0,
        };

        byte[] buffer = new byte[totalSize];
        MemoryMarshal.Write(buffer.AsSpan(0, ReadRequestHeaderSize), in header);
        WriteItems(buffer.AsSpan(ReadRequestHeaderSize), wireItems);
        return buffer;
    }

    public static byte[] EncodeWriteRequest(
        uint targetProcessId,
        IReadOnlyList<BatchWriteRequestItem> items)
    {
        ValidateEncoderItemCount(items);

        int itemsSize = checked(ItemSize * items.Count);
        int dataOffset = checked(WriteRequestHeaderSize + itemsSize);
        uint aggregateSize = 0;
        WriteBatchItem[] wireItems = new WriteBatchItem[items.Count];

        for (int index = 0; index < items.Count; index++)
        {
            BatchWriteRequestItem item = items[index];
            uint itemSize = checked((uint)item.Data.Length);
            ValidateEncoderItemSize(itemSize, nameof(items));

            wireItems[index] = new WriteBatchItem
            {
                Address = item.Address,
                Size = itemSize,
                DataOffset = checked((uint)dataOffset + aggregateSize),
            };

            aggregateSize = checked(aggregateSize + itemSize);
            if (aggregateSize > ProtocolConstants.MaxBatchPayloadSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(items),
                    "Aggregate write payload exceeds the protocol limit.");
            }
        }

        int totalSize = checked(dataOffset + checked((int)aggregateSize));
        WriteBatchRequestHeader header = new()
        {
            Header = CreateCommonRequestHeader(totalSize),
            TargetProcessId = targetProcessId,
            ItemCount = checked((uint)items.Count),
            ItemsOffset = WriteRequestHeaderSize,
            DataOffset = checked((uint)dataOffset),
            DataSize = aggregateSize,
            Reserved = 0,
        };

        byte[] buffer = new byte[totalSize];
        MemoryMarshal.Write(buffer.AsSpan(0, WriteRequestHeaderSize), in header);
        WriteItems(buffer.AsSpan(WriteRequestHeaderSize, itemsSize), wireItems);

        int destinationOffset = dataOffset;
        foreach (BatchWriteRequestItem item in items)
        {
            item.Data.Span.CopyTo(buffer.AsSpan(destinationOffset));
            destinationOffset = checked(destinationOffset + item.Data.Length);
        }

        return buffer;
    }

    public static OperationStatus ValidateReadRequest(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < ReadRequestHeaderSize)
        {
            return OperationStatus.InvalidRequest;
        }

        ReadBatchRequestHeader header = MemoryMarshal.Read<ReadBatchRequestHeader>(buffer);
        OperationStatus status = ValidateCommonRequestHeader(header.Header, buffer.Length);
        if (status != OperationStatus.Success)
        {
            return status;
        }

        if (header.Reserved != 0)
        {
            return OperationStatus.InvalidReservedField;
        }

        if (header.ItemCount == 0 || header.ItemCount > ProtocolConstants.MaxBatchItems)
        {
            return OperationStatus.InvalidItemCount;
        }

        if (header.ItemsOffset != ReadRequestHeaderSize)
        {
            return OperationStatus.InvalidOffset;
        }

        uint expectedSize;
        try
        {
            expectedSize = checked(
                header.ItemsOffset + checked(header.ItemCount * checked((uint)ItemSize)));
        }
        catch (OverflowException)
        {
            return OperationStatus.InvalidOffset;
        }

        if (expectedSize != buffer.Length)
        {
            return OperationStatus.InvalidRequest;
        }

        uint aggregateSize = 0;
        for (uint index = 0; index < header.ItemCount; index++)
        {
            int itemOffset = checked((int)(header.ItemsOffset + (index * ItemSize)));
            ReadBatchItem item = MemoryMarshal.Read<ReadBatchItem>(buffer[itemOffset..]);

            if (item.ResultOffset != aggregateSize)
            {
                return OperationStatus.InvalidOffset;
            }

            if (IsValidItemSize(item.Size))
            {
                try
                {
                    aggregateSize = checked(aggregateSize + item.Size);
                }
                catch (OverflowException)
                {
                    return OperationStatus.AggregateLimitExceeded;
                }

                if (aggregateSize > ProtocolConstants.MaxBatchPayloadSize)
                {
                    return OperationStatus.AggregateLimitExceeded;
                }
            }
        }

        return OperationStatus.Success;
    }

    public static OperationStatus ValidateWriteRequest(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < WriteRequestHeaderSize)
        {
            return OperationStatus.InvalidRequest;
        }

        WriteBatchRequestHeader header = MemoryMarshal.Read<WriteBatchRequestHeader>(buffer);
        OperationStatus status = ValidateCommonRequestHeader(header.Header, buffer.Length);
        if (status != OperationStatus.Success)
        {
            return status;
        }

        if (header.Reserved != 0)
        {
            return OperationStatus.InvalidReservedField;
        }

        if (header.ItemCount == 0 || header.ItemCount > ProtocolConstants.MaxBatchItems)
        {
            return OperationStatus.InvalidItemCount;
        }

        if (header.ItemsOffset != WriteRequestHeaderSize)
        {
            return OperationStatus.InvalidOffset;
        }

        uint expectedDataOffset;
        try
        {
            expectedDataOffset = checked(
                header.ItemsOffset + checked(header.ItemCount * checked((uint)ItemSize)));
        }
        catch (OverflowException)
        {
            return OperationStatus.InvalidOffset;
        }

        if (header.DataOffset != expectedDataOffset)
        {
            return OperationStatus.InvalidOffset;
        }

        if (header.DataSize > ProtocolConstants.MaxBatchPayloadSize)
        {
            return OperationStatus.AggregateLimitExceeded;
        }

        uint expectedTotalSize;
        try
        {
            expectedTotalSize = checked(header.DataOffset + header.DataSize);
        }
        catch (OverflowException)
        {
            return OperationStatus.InvalidOffset;
        }

        if (expectedTotalSize != buffer.Length)
        {
            return OperationStatus.InvalidRequest;
        }

        uint aggregateSize = 0;
        for (uint index = 0; index < header.ItemCount; index++)
        {
            int itemOffset = checked((int)(header.ItemsOffset + (index * ItemSize)));
            WriteBatchItem item = MemoryMarshal.Read<WriteBatchItem>(buffer[itemOffset..]);
            uint expectedItemDataOffset;

            try
            {
                expectedItemDataOffset = checked(header.DataOffset + aggregateSize);
            }
            catch (OverflowException)
            {
                return OperationStatus.InvalidOffset;
            }

            if (item.DataOffset != expectedItemDataOffset)
            {
                return OperationStatus.InvalidOffset;
            }

            if (IsValidItemSize(item.Size))
            {
                try
                {
                    aggregateSize = checked(aggregateSize + item.Size);
                }
                catch (OverflowException)
                {
                    return OperationStatus.AggregateLimitExceeded;
                }

                if (aggregateSize > ProtocolConstants.MaxBatchPayloadSize)
                {
                    return OperationStatus.AggregateLimitExceeded;
                }
            }
        }

        return aggregateSize == header.DataSize
            ? OperationStatus.Success
            : OperationStatus.InvalidRequest;
    }

    public static byte[] EncodeReadResponse(
        ReadBatchResponseHeader header,
        IReadOnlyList<BatchItemResult> results,
        ReadOnlySpan<byte> data)
    {
        if (results.Count > ProtocolConstants.MaxBatchItems)
        {
            throw new ArgumentOutOfRangeException(nameof(results));
        }

        if (data.Length > ProtocolConstants.MaxBatchPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        int resultsSize = checked(ItemResultSize * results.Count);
        int totalSize = checked(ReadResponseHeaderSize + resultsSize + data.Length);
        if (header.ItemCount != results.Count ||
            header.ResultsOffset != ReadResponseHeaderSize ||
            header.DataOffset != ReadResponseHeaderSize + resultsSize ||
            header.DataSize != data.Length)
        {
            throw new ArgumentException("Read response offsets do not match the supplied data.", nameof(header));
        }

        byte[] buffer = new byte[totalSize];
        MemoryMarshal.Write(buffer.AsSpan(0, ReadResponseHeaderSize), in header);
        WriteItems(buffer.AsSpan(ReadResponseHeaderSize, resultsSize), results);
        data.CopyTo(buffer.AsSpan(ReadResponseHeaderSize + resultsSize));
        return buffer;
    }

    public static BatchReadResponseMessage DecodeReadResponse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < ReadResponseHeaderSize)
        {
            throw new ArgumentException("Batch read response is too small.", nameof(buffer));
        }

        ReadBatchResponseHeader header = MemoryMarshal.Read<ReadBatchResponseHeader>(buffer);
        if (header.ItemCount > ProtocolConstants.MaxBatchItems ||
            header.ResultsOffset != ReadResponseHeaderSize ||
            header.DataSize > ProtocolConstants.MaxBatchPayloadSize)
        {
            throw new InvalidDataException("Batch read response header is invalid.");
        }

        uint expectedDataOffset = checked(
            header.ResultsOffset + checked(header.ItemCount * checked((uint)ItemResultSize)));
        uint expectedTotalSize = checked(expectedDataOffset + header.DataSize);
        if (header.DataOffset != expectedDataOffset || expectedTotalSize != buffer.Length)
        {
            throw new InvalidDataException("Batch read response offsets are invalid.");
        }

        BatchItemResult[] results = ReadItemResults(
            buffer,
            header.ResultsOffset,
            header.ItemCount);
        return new BatchReadResponseMessage(
            header,
            results,
            buffer[(int)header.DataOffset..].ToArray());
    }

    public static byte[] EncodeWriteResponse(
        WriteBatchResponseHeader header,
        IReadOnlyList<BatchItemResult> results)
    {
        if (results.Count > ProtocolConstants.MaxBatchItems ||
            header.ItemCount != results.Count ||
            header.ResultsOffset != WriteResponseHeaderSize)
        {
            throw new ArgumentException("Write response offsets do not match the supplied results.", nameof(header));
        }

        int resultsSize = checked(ItemResultSize * results.Count);
        byte[] buffer = new byte[checked(WriteResponseHeaderSize + resultsSize)];
        MemoryMarshal.Write(buffer.AsSpan(0, WriteResponseHeaderSize), in header);
        WriteItems(buffer.AsSpan(WriteResponseHeaderSize), results);
        return buffer;
    }

    public static BatchWriteResponseMessage DecodeWriteResponse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < WriteResponseHeaderSize)
        {
            throw new ArgumentException("Batch write response is too small.", nameof(buffer));
        }

        WriteBatchResponseHeader header = MemoryMarshal.Read<WriteBatchResponseHeader>(buffer);
        if (header.ItemCount > ProtocolConstants.MaxBatchItems ||
            header.ResultsOffset != WriteResponseHeaderSize)
        {
            throw new InvalidDataException("Batch write response header is invalid.");
        }

        uint expectedSize = checked(
            header.ResultsOffset + checked(header.ItemCount * checked((uint)ItemResultSize)));
        if (expectedSize != buffer.Length)
        {
            throw new InvalidDataException("Batch write response length is invalid.");
        }

        return new BatchWriteResponseMessage(
            header,
            ReadItemResults(buffer, header.ResultsOffset, header.ItemCount));
    }

    private static CommonRequestHeader CreateCommonRequestHeader(int structureSize) =>
        new()
        {
            ProtocolVersion = ProtocolConstants.CurrentProtocolVersion,
            StructureSize = checked((uint)structureSize),
            Flags = 0,
            Reserved = 0,
        };

    private static OperationStatus ValidateCommonRequestHeader(
        CommonRequestHeader header,
        int actualSize)
    {
        if (header.ProtocolVersion.Major != ProtocolConstants.ProtocolMajor ||
            header.ProtocolVersion.Minor != ProtocolConstants.ProtocolMinor)
        {
            return OperationStatus.ProtocolMismatch;
        }

        if (header.Flags != 0)
        {
            return OperationStatus.InvalidFlags;
        }

        if (header.Reserved != 0)
        {
            return OperationStatus.InvalidReservedField;
        }

        return header.StructureSize == actualSize
            ? OperationStatus.Success
            : OperationStatus.InvalidStructureSize;
    }

    private static bool IsValidItemSize(uint size) =>
        size > 0 && size <= ProtocolConstants.MaxSingleItemSize;

    private static void ValidateEncoderItemCount<T>(IReadOnlyCollection<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0 || items.Count > ProtocolConstants.MaxBatchItems)
        {
            throw new ArgumentOutOfRangeException(
                nameof(items),
                $"Batch item count must be between 1 and {ProtocolConstants.MaxBatchItems}.");
        }
    }

    private static void ValidateEncoderItemSize(uint size, string parameterName)
    {
        if (!IsValidItemSize(size))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Each batch item must contain between 1 and {ProtocolConstants.MaxSingleItemSize} bytes.");
        }
    }

    private static void WriteItems<T>(Span<byte> destination, IReadOnlyList<T> items)
        where T : unmanaged
    {
        int itemSize = Marshal.SizeOf<T>();
        for (int index = 0; index < items.Count; index++)
        {
            T item = items[index];
            MemoryMarshal.Write(destination.Slice(index * itemSize, itemSize), in item);
        }
    }

    private static BatchItemResult[] ReadItemResults(
        ReadOnlySpan<byte> buffer,
        uint resultsOffset,
        uint itemCount)
    {
        BatchItemResult[] results = new BatchItemResult[itemCount];
        for (uint index = 0; index < itemCount; index++)
        {
            int offset = checked((int)(resultsOffset + (index * ItemResultSize)));
            results[index] = MemoryMarshal.Read<BatchItemResult>(buffer[offset..]);
        }

        return results;
    }
}
