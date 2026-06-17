namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// The kind of operation a rule proposes over a datapoint (Story 8.14).
/// </summary>
public enum ProposedOpKind
{
    /// <summary>Link the datapoint to an existing event.</summary>
    Link,

    /// <summary>Mark the datapoint intentionally ignored (no event).</summary>
    Ignore,

    /// <summary>Create a new candidate event and link the datapoint to it.</summary>
    GenerateCandidate
}
