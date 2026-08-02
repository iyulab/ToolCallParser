using System.Text.Json;

namespace ToolCallParser;

/// <summary>
/// JSON access helpers shared by the parsers.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Object-safe property lookup: returns <c>false</c> when <paramref name="element"/> is not a
    /// JSON object, instead of throwing.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> is not a total function —
    /// despite the <c>Try</c> prefix it throws <see cref="InvalidOperationException"/> unless the
    /// receiver is an object. Parsers walk property chains over payloads whose shape is unknown by
    /// definition (that is what detection is for), so every step of such a chain is a potential
    /// fault. Two different providers reusing one container name under different kinds is enough:
    /// an <c>output</c> array and an <c>output</c> object both exist in the wild.
    /// <para>
    /// Detection must be able to answer "not mine" for any input. Use this for every property read
    /// whose receiver has not already been proven to be an object.
    /// </para>
    /// </remarks>
    internal static bool TryGetObjectProperty(this JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        return element.TryGetProperty(propertyName, out value);
    }
}
