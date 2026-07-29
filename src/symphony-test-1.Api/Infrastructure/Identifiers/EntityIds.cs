using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Dapper;

namespace SymphonyTest1.Api.Infrastructure.Identifiers;

/// <summary>Strongly identifies a language inside the application.</summary>
[JsonConverter(typeof(LanguageIdJsonConverter))]
public readonly record struct LanguageId(Guid Value) :
    IParsable<LanguageId>,
    ISpanFormattable
{
    public static LanguageId Parse(string s, IFormatProvider? provider) =>
        new(Guid.Parse(s, provider));

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out LanguageId result)
    {
        if (Guid.TryParse(s, provider, out var parsed))
        {
            result = new LanguageId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format);
}

/// <summary>Strongly identifies a greeting inside the application.</summary>
[JsonConverter(typeof(GreetingIdJsonConverter))]
public readonly record struct GreetingId(Guid Value) :
    IParsable<GreetingId>,
    ISpanFormattable
{
    public static GreetingId Parse(string s, IFormatProvider? provider) =>
        new(Guid.Parse(s, provider));

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out GreetingId result)
    {
        if (Guid.TryParse(s, provider, out var parsed))
        {
            result = new GreetingId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        Value.ToString(format, formatProvider);

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) =>
        Value.TryFormat(destination, out charsWritten, format);
}

internal static class EntityIdTypeHandlers
{
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new LanguageIdTypeHandler());
        SqlMapper.AddTypeHandler(new GreetingIdTypeHandler());
    }

    private abstract class EntityIdTypeHandler<TId> : SqlMapper.TypeHandler<TId>
        where TId : struct
    {
        public sealed override void SetValue(IDbDataParameter parameter, TId value)
        {
            parameter.DbType = DbType.Guid;
            parameter.Value = GetValue(value);
        }

        protected abstract Guid GetValue(TId value);
    }

    private sealed class LanguageIdTypeHandler : EntityIdTypeHandler<LanguageId>
    {
        public override LanguageId Parse(object value) => new((Guid)value);

        protected override Guid GetValue(LanguageId value) => value.Value;
    }

    private sealed class GreetingIdTypeHandler : EntityIdTypeHandler<GreetingId>
    {
        public override GreetingId Parse(object value) => new((Guid)value);

        protected override Guid GetValue(GreetingId value) => value.Value;
    }
}

internal sealed class LanguageIdJsonConverter : JsonConverter<LanguageId>
{
    public override LanguageId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        LanguageId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

internal sealed class GreetingIdJsonConverter : JsonConverter<GreetingId>
{
    public override GreetingId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(
        Utf8JsonWriter writer,
        GreetingId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
