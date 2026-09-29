using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentIntelligence.Infrastructure.Persistence.Configurations;

internal static class JsonColumnExtensions
{
    // Enums as names, so reordering an enum never changes the meaning of stored rows.
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Stores a list of value objects in a jsonb column. The value objects are deserialized
    /// through their public constructors, so their invariants are checked on every read.
    /// </summary>
    public static PropertyBuilder<IReadOnlyList<T>> HasJsonConversion<T>(this PropertyBuilder<IReadOnlyList<T>> property) =>
        property
            .HasConversion(
                value => Serialize(value),
                json => Deserialize<T>(json),
                new ValueComparer<IReadOnlyList<T>>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    value => value.ToList()))
            .HasColumnType("jsonb");

    private static string Serialize<T>(IReadOnlyList<T> value) => JsonSerializer.Serialize(value, _options);

    private static List<T> Deserialize<T>(string json) => JsonSerializer.Deserialize<List<T>>(json, _options) ?? [];
}
