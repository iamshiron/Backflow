using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Shiron.Backflow.Ext.AspNetCore;

var destination = args.Single();
Directory.CreateDirectory(destination);
var types = typeof(BackflowGraph).Assembly.GetExportedTypes()
    .Where(type => type.IsEnum || type.IsSealed && type.GetMethod("<Clone>$") is not null && type != typeof(BackflowExecutionRequest))
    .OrderBy(type => type.Name).ToArray();
var nullability = new NullabilityInfoContext();
var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(), RespectRequiredConstructorParameters = true };
var text = new System.Text.StringBuilder("// Generated from Backflow.Ext.AspNetCore. Run pnpm generate; do not edit.\n");
text.AppendLine("export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue }; ");
foreach (var type in types) {
    if (type.IsEnum) {
        text.AppendLine($"export enum {type.Name} {{");
        foreach (var name in Enum.GetNames(type)) text.AppendLine($"  {name} = {Convert.ToInt32(Enum.Parse(type, name))},");
        text.AppendLine("}");
        continue;
    }
    text.AppendLine($"export interface {type.Name} {{");
    foreach (var property in type.GetProperties()) {
        var nullable = nullability.Create(property).ReadState == NullabilityState.Nullable;
        text.AppendLine($"  {JsonNamingPolicy.CamelCase.ConvertName(property.Name)}: {Map(property.PropertyType)}{(nullable && Nullable.GetUnderlyingType(property.PropertyType) is null ? " | null" : "")};");
    }
    text.AppendLine("}");
}
File.WriteAllText(Path.Combine(destination, "contracts.ts"), text.ToString());
var schemas = new JsonObject();
foreach (var type in types.Where(type => !type.IsEnum)) schemas[type.Name] = serializer.GetJsonSchemaAsNode(type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
File.WriteAllText(Path.Combine(destination, "schemas.json"), schemas.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

static string Map(Type type) => type switch {
    _ when Nullable.GetUnderlyingType(type) is { } underlying => $"{Map(underlying)} | null",
    _ when type.IsArray => $"{Map(type.GetElementType()!)}[]",
    _ when type == typeof(string) => "string",
    _ when type == typeof(bool) => "boolean",
    _ when type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type) => "JsonValue",
    _ when type.IsPrimitive || type == typeof(decimal) => "number",
    _ => type.Name
};
