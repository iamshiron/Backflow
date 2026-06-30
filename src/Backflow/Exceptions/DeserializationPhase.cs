namespace Shiron.Backflow.Exceptions;

/// <summary>
/// Identifies the stage of pipeline deserialization at which an error was detected.
/// </summary>
public enum DeserializationPhase {
    /// <summary>The raw JSON could not be parsed by the JSON serializer.</summary>
    JsonParsing,

    /// <summary>The DTO has structural problems: null/empty fields, duplicate IDs, dangling edge references.</summary>
    Structure,

    /// <summary>A node type referenced in the DTO is not registered or could not be instantiated.</summary>
    NodeResolution,

    /// <summary>A .NET type name (generic argument or input type) could not be resolved at runtime.</summary>
    TypeResolution,

    /// <summary>A port referenced in an edge or input does not exist on the target node.</summary>
    PortResolution,

    /// <summary>One or more edges would introduce a cycle in the pipeline DAG.</summary>
    Graph,

    /// <summary>An input value could not be deserialized or converted to the declared port type.</summary>
    ValueResolution,
}
