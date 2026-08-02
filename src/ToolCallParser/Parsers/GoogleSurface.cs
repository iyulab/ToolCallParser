namespace ToolCallParser.Parsers;

/// <summary>
/// Which Google tool-calling surface a payload is being written for.
/// </summary>
/// <remarks>
/// Google serves tool calling through two wire formats that differ in both directions, and a
/// response body carries nothing that identifies which one produced it. Reading tolerates that —
/// each shape is recognisable on sight — but writing cannot: the caller is the only party that
/// knows where the message is going, so it has to say.
/// </remarks>
public enum GoogleSurface
{
    /// <summary>
    /// generateContent: results are sent as <c>functionResponse</c> parts. Call ids are generated
    /// locally because this surface does not supply one.
    /// </summary>
    GenerateContent = 0,

    /// <summary>
    /// Interactions: results are sent as <c>function_result</c> steps carrying the <c>call_id</c>
    /// that matches the originating call's <c>id</c>.
    /// </summary>
    Interactions = 1
}
