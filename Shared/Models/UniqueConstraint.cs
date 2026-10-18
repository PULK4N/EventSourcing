using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EventSourcing.Shared.Models;

/// <summary>
/// A value that no two aggregates with the same state-data type may hold. Derive one type per
/// constraint; the derived type name becomes <see cref="ConstraintName"/>. Only the hash is
/// stored, because the value can always be recalculated from the state data. Rows are stored
/// and read back as this base type.
/// </summary>
public class UniqueConstraint
{
    public long Id { get; private set; }
    public Guid AggregateId { get; private set; }
    public string ConstraintName { get; private set; } = string.Empty;
    public byte[] Hash { get; private set; } = [ ];

    protected UniqueConstraint() { }

    /// <param name="value">
    /// The unique value. A constraint over several values must join them unambiguously, for
    /// example as a JSON array, so that ("ab", "c") and ("a", "bc") stay different.
    /// </param>
    protected UniqueConstraint(ISharedStateData stateData, string value)
    {
        AggregateId = stateData.Id.Value;
        ConstraintName = GetType().Name;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashComponent(hash, stateData.GetType().Name);
        AppendHashComponent(hash, ConstraintName);
        AppendHashComponent(hash, value);
        Hash = hash.GetHashAndReset();
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
