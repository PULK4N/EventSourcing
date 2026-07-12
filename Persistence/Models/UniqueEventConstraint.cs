using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using EventSourcing.Shared.Models;
using Microsoft.IdentityModel.Tokens;

namespace EventSourcing.Persistence.Models;

public class UniqueEventConstraint
{
    public required byte[] ConstraintHash { get; set; } = [ ];
    public required Guid AggregateId { get; set; }
    public required uint OrderNumber { get; set; }
    public required string ConstraintName { get; set; } = string.Empty;
    public required string StateMachineId { get; set; } = string.Empty;

    private UniqueEventConstraint() { }

    [SetsRequiredMembers]
    public UniqueEventConstraint(EventPayload payload, UniqueEventConstraintData constraint)
    {
        if (constraint.ValueToHash.Trim().IsNullOrEmpty())
            throw new ArgumentNullException(nameof(constraint.ValueToHash));

        var executionInfo = payload.EventExecutionInfo;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashComponent(hash, executionInfo.StateMachineId);
        AppendHashComponent(hash, constraint.ConstraintName);
        AppendHashComponent(hash, constraint.ValueToHash);

        ConstraintHash = hash.GetHashAndReset();
        AggregateId = executionInfo.AggregateId;
        OrderNumber = executionInfo.OrderNumber;
        ConstraintName = constraint.ConstraintName;
        StateMachineId = executionInfo.StateMachineId;
    }

    private static void AppendHashComponent(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);

        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
