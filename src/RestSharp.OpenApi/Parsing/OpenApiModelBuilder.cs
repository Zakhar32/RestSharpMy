//  Copyright (c) .NET Foundation and Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Text.Json;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Parsing;

/// <summary>
/// Converts a parsed OpenAPI <see cref="JsonElement"/> into the resolved semantic
/// <see cref="OpenApiDocumentModel"/>. References are resolved into shared <see cref="ApiSchema"/>
/// instances, so the resulting graph may contain cycles. Supports the subset of OpenAPI 3.0 / 3.1
/// that maps cleanly onto an HTTP client: paths, operations, parameters, request bodies, responses,
/// component schemas, validation constraints and polymorphism (<c>allOf</c>/<c>oneOf</c>/<c>anyOf</c>
/// and discriminators).
/// </summary>
sealed class OpenApiModelBuilder {
    readonly JsonElement                    _root;
    readonly OpenApiDocumentModel           _model = new();
    readonly Dictionary<string, ApiSchema>  _schemasByRef = new(StringComparer.Ordinal);

    OpenApiModelBuilder(JsonElement root) => _root = root;

    public static OpenApiDocumentModel Build(JsonElement root, string sourceHash) {
        var builder = new OpenApiModelBuilder(root);
        builder.BuildModel();
        builder._model.SourceHash = sourceHash;
        return builder._model;
    }

    void BuildModel() {
        ReadInfo();
        ReadServers();
        PreRegisterNamedSchemas();
        PopulateNamedSchemas();
        ReadPaths();
    }

    void ReadInfo() {
        if (!_root.TryGetProperty("info", out var info)) return;

        _model.Title      = GetString(info, "title") ?? "";
        _model.ApiVersion = GetString(info, "version") ?? "";
    }

    void ReadServers() {
        if (!_root.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array) return;

        foreach (var server in servers.EnumerateArray()) {
            var url = GetString(server, "url");
            if (string.IsNullOrEmpty(url)) continue;

            // We only keep the path portion; the RestClient's BaseUrl owns scheme + host.
            _model.BasePath = ExtractBasePath(url!);
            return;
        }
    }

    static string ExtractBasePath(string url) {
        // Server URLs may be absolute (https://host/v1), relative (/v1) or contain {variables}.
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) return absolute.AbsolutePath.Trim('/');

        return url.Trim('/');
    }

    // --- Schemas -------------------------------------------------------------------------------

    void PreRegisterNamedSchemas() {
        foreach (var (name, _) in EnumerateComponentSchemas()) {
            var schema = new ApiSchema { Name = name };
            _model.Schemas[name]      = schema;
            _schemasByRef[$"#/components/schemas/{name}"] = schema;
        }
    }

    void PopulateNamedSchemas() {
        foreach (var (name, element) in EnumerateComponentSchemas()) {
            PopulateSchema(_model.Schemas[name], element);
        }
    }

    IEnumerable<(string Name, JsonElement Element)> EnumerateComponentSchemas() {
        if (!_root.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var schemas)  ||
            schemas.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var property in schemas.EnumerateObject()) yield return (property.Name, property.Value);
    }

    /// <summary>
    /// Resolves a schema element to an <see cref="ApiSchema"/>. A <c>$ref</c> to a named component
    /// returns the shared instance; anything else is built inline.
    /// </summary>
    ApiSchema ResolveSchema(JsonElement element) {
        if (TryGetRef(element, out var refPath)) {
            if (_schemasByRef.TryGetValue(refPath!, out var named)) return named;

            // A $ref to something other than a pre-registered component schema (e.g. an inline ref
            // target). Resolve the pointer and build it inline.
            if (TryResolvePointer(refPath!, out var target)) return BuildInlineSchema(target);

            throw new OpenApiParseException($"Unable to resolve schema reference '{refPath}'.");
        }

        return BuildInlineSchema(element);
    }

    ApiSchema BuildInlineSchema(JsonElement element) {
        var schema = new ApiSchema();
        PopulateSchema(schema, element);
        return schema;
    }

    void PopulateSchema(ApiSchema schema, JsonElement element) {
        if (element.ValueKind != JsonValueKind.Object) {
            schema.Kind = SchemaKind.Unknown;
            return;
        }

        schema.Description = GetString(element, "description");
        ReadNullable(schema, element);
        ReadConstraints(schema, element);

        // Composition / polymorphism first - these win over a plain type.
        if (element.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array) {
            BuildAllOf(schema, allOf, element);
            return;
        }

        if ((element.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array) ||
            (element.TryGetProperty("anyOf", out oneOf)     && oneOf.ValueKind == JsonValueKind.Array)) {
            BuildComposite(schema, oneOf, element);
            return;
        }

        if (element.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array) {
            BuildEnum(schema, element, enumValues);
            return;
        }

        var type = GetSchemaType(element);

        switch (type) {
            case "array":
                schema.Kind  = SchemaKind.Array;
                schema.Items = element.TryGetProperty("items", out var items) ? ResolveSchema(items) : AnySchema();
                break;
            case "object":
            case null when element.TryGetProperty("properties", out _):
                BuildObjectOrMap(schema, element);
                break;
            case "string":
            case "integer":
            case "number":
            case "boolean":
                schema.Kind      = SchemaKind.Primitive;
                schema.Primitive = MapPrimitive(type!);
                schema.Format    = GetString(element, "format");
                break;
            case null:
                // No type and no properties: a free-form object if additionalProperties present, else any.
                if (element.TryGetProperty("additionalProperties", out _)) BuildObjectOrMap(schema, element);
                else schema.Kind = SchemaKind.Unknown;
                break;
            default:
                schema.Kind = SchemaKind.Unknown;
                break;
        }
    }

    void BuildObjectOrMap(ApiSchema schema, JsonElement element) {
        var hasProperties = element.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object;
        var hasAdditional = element.TryGetProperty("additionalProperties", out var additional);

        if (!hasProperties && hasAdditional && additional.ValueKind != JsonValueKind.False) {
            schema.Kind                 = SchemaKind.Map;
            schema.AdditionalProperties = additional.ValueKind == JsonValueKind.True || additional.ValueKind == JsonValueKind.Undefined
                ? AnySchema()
                : ResolveSchema(additional);
            return;
        }

        schema.Kind = SchemaKind.Object;
        ReadDiscriminator(schema, element);

        if (hasProperties) ReadProperties(schema, properties, element);
    }

    void ReadProperties(ApiSchema schema, JsonElement properties, JsonElement owner) {
        var required = ReadRequiredSet(owner);

        foreach (var property in properties.EnumerateObject()) {
            var propertySchema = ResolveSchema(property.Value);
            schema.Properties.Add(new ApiProperty(property.Name, propertySchema, required.Contains(property.Name)));
        }
    }

    static HashSet<string> ReadRequiredSet(JsonElement owner) {
        var required = new HashSet<string>(StringComparer.Ordinal);

        if (owner.TryGetProperty("required", out var requiredArray) && requiredArray.ValueKind == JsonValueKind.Array) {
            foreach (var item in requiredArray.EnumerateArray()) {
                if (item.ValueKind == JsonValueKind.String) required.Add(item.GetString()!);
            }
        }

        return required;
    }

    /// <summary>
    /// <c>allOf</c> means "all of these schemas at once". We model it as an object: if exactly one
    /// entry is a named object schema we treat it as a base class (CLR inheritance), and merge the
    /// remaining inline entries' properties in. Otherwise we flatten everything into one object.
    /// </summary>
    void BuildAllOf(ApiSchema schema, JsonElement allOf, JsonElement owner) {
        schema.Kind = SchemaKind.Object;
        ReadDiscriminator(schema, owner);

        var required        = ReadRequiredSet(owner);
        var namedBaseFound  = false;

        foreach (var part in allOf.EnumerateArray()) {
            if (TryGetRef(part, out var refPath) && _schemasByRef.TryGetValue(refPath!, out var named)) {
                if (!namedBaseFound) {
                    schema.BaseSchema = named;
                    namedBaseFound    = true;
                    continue;
                }

                // A second named base - flatten its properties since CLR has single inheritance.
                MergeProperties(schema, named, required);
                continue;
            }

            // Inline part: merge its properties and required directly.
            var inline = ResolveSchema(part);
            MergeProperties(schema, inline, required);
            if (inline.Discriminator != null && schema.Discriminator == null) schema.Discriminator = inline.Discriminator;
        }

        // Properties declared directly alongside allOf.
        if (owner.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object) {
            ReadProperties(schema, properties, owner);
        }

        // Apply any required names that referenced inherited/merged properties.
        ApplyRequired(schema, required);
    }

    static void MergeProperties(ApiSchema target, ApiSchema source, HashSet<string> required) {
        foreach (var property in source.Properties) {
            if (target.Properties.Any(p => p.Name == property.Name)) continue;
            target.Properties.Add(new ApiProperty(property.Name, property.Schema, property.Required || required.Contains(property.Name)));
        }
    }

    static void ApplyRequired(ApiSchema schema, HashSet<string> required) {
        foreach (var property in schema.Properties) {
            if (required.Contains(property.Name)) property.Required = true;
        }
    }

    void BuildComposite(ApiSchema schema, JsonElement composition, JsonElement owner) {
        schema.Kind = SchemaKind.Composite;
        ReadDiscriminator(schema, owner);

        foreach (var part in composition.EnumerateArray()) schema.Composition.Add(ResolveSchema(part));
    }

    void BuildEnum(ApiSchema schema, JsonElement element, JsonElement enumValues) {
        schema.Kind      = SchemaKind.Enum;
        var type         = GetSchemaType(element);
        schema.Primitive = type != null && type != "object" && type != "array" ? MapPrimitive(type) : PrimitiveType.String;
        schema.Format    = GetString(element, "format");

        foreach (var value in enumValues.EnumerateArray()) {
            if (value.ValueKind == JsonValueKind.Null) {
                schema.Nullable = true;
                continue;
            }

            schema.EnumValues.Add(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
        }
    }

    void ReadDiscriminator(ApiSchema schema, JsonElement element) {
        if (!element.TryGetProperty("discriminator", out var discriminator) || discriminator.ValueKind != JsonValueKind.Object) return;

        var propertyName = GetString(discriminator, "propertyName");
        if (string.IsNullOrEmpty(propertyName)) return;

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        if (discriminator.TryGetProperty("mapping", out var mappingElement) && mappingElement.ValueKind == JsonValueKind.Object) {
            foreach (var entry in mappingElement.EnumerateObject()) {
                var target = entry.Value.GetString();
                if (target != null) mapping[entry.Name] = SchemaNameFromRef(target);
            }
        }

        schema.Discriminator = new DiscriminatorInfo(propertyName!, mapping);
    }

    void ReadNullable(ApiSchema schema, JsonElement element) {
        // OpenAPI 3.0: nullable: true. OpenAPI 3.1: type can be an array including "null".
        if (element.TryGetProperty("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True) schema.Nullable = true;

        if (element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array) {
            foreach (var entry in type.EnumerateArray()) {
                if (entry.ValueKind == JsonValueKind.String && entry.GetString() == "null") schema.Nullable = true;
            }
        }
    }

    void ReadConstraints(ApiSchema schema, JsonElement element) {
        var c = schema.Constraints;

        c.Minimum    = GetDouble(element, "minimum");
        c.Maximum    = GetDouble(element, "maximum");
        c.MultipleOf = GetDouble(element, "multipleOf");
        c.MinLength  = GetInt(element, "minLength");
        c.MaxLength  = GetInt(element, "maxLength");
        c.Pattern    = GetString(element, "pattern");
        c.MinItems   = GetInt(element, "minItems");
        c.MaxItems   = GetInt(element, "maxItems");

        if (element.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True) c.UniqueItems = true;

        ReadExclusiveBound(element, "exclusiveMinimum", v => c.ExclusiveMinimum = true, v => { c.Minimum = v; c.ExclusiveMinimum = true; });
        ReadExclusiveBound(element, "exclusiveMaximum", v => c.ExclusiveMaximum = true, v => { c.Maximum = v; c.ExclusiveMaximum = true; });

        if (element.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array) {
            var allowed = new List<string>();

            foreach (var value in enumValues.EnumerateArray()) {
                if (value.ValueKind == JsonValueKind.Null) continue;
                allowed.Add(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
            }

            if (allowed.Count > 0) c.AllowedValues = allowed;
        }
    }

    static void ReadExclusiveBound(JsonElement element, string name, Action<bool> onBool, Action<double> onNumber) {
        if (!element.TryGetProperty(name, out var value)) return;

        if (value.ValueKind == JsonValueKind.True) onBool(true);                    // OpenAPI 3.0
        else if (value.ValueKind == JsonValueKind.Number) onNumber(value.GetDouble()); // OpenAPI 3.1
    }

    // --- Paths & operations --------------------------------------------------------------------

    void ReadPaths() {
        if (!_root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object) return;

        foreach (var path in paths.EnumerateObject()) {
            var pathItem = path.Value;
            if (pathItem.ValueKind != JsonValueKind.Object) continue;

            var pathLevelParameters = ReadParameters(pathItem);

            foreach (var member in pathItem.EnumerateObject()) {
                if (!TryMapMethod(member.Name, out var method)) continue;

                var operation = BuildOperation(path.Name, method, member.Value, pathLevelParameters);
                _model.Operations.Add(operation);
            }
        }
    }

    ApiOperation BuildOperation(string path, Method method, JsonElement element, List<ApiParameter> pathLevelParameters) {
        var operation = new ApiOperation(GetString(element, "operationId"), method, path.TrimStart('/')) {
            Summary     = GetString(element, "summary"),
            Description = GetString(element, "description")
        };

        if (element.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array) {
            foreach (var tag in tags.EnumerateArray()) {
                if (tag.ValueKind == JsonValueKind.String) operation.Tags.Add(tag.GetString()!);
            }
        }

        // Merge path-level and operation-level parameters; operation-level wins on (name, location).
        var operationParameters = ReadParameters(element);
        var merged              = MergeParameters(pathLevelParameters, operationParameters);
        foreach (var parameter in merged) operation.Parameters.Add(parameter);

        if (element.TryGetProperty("requestBody", out var requestBody)) operation.RequestBody = ReadRequestBody(requestBody);

        if (element.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Object) {
            foreach (var response in responses.EnumerateObject()) operation.Responses.Add(ReadResponse(response.Name, response.Value));
        }

        return operation;
    }

    static List<ApiParameter> MergeParameters(List<ApiParameter> pathLevel, List<ApiParameter> operationLevel) {
        var merged = new List<ApiParameter>(operationLevel);

        foreach (var parameter in pathLevel) {
            if (!operationLevel.Any(p => p.Name == parameter.Name && p.Location == parameter.Location)) merged.Add(parameter);
        }

        return merged;
    }

    List<ApiParameter> ReadParameters(JsonElement owner) {
        var result = new List<ApiParameter>();

        if (!owner.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array) return result;

        foreach (var element in parameters.EnumerateArray()) {
            var resolved = ResolveComponent(element, "parameters");
            var parameter = ReadParameter(resolved);
            if (parameter != null) result.Add(parameter);
        }

        return result;
    }

    ApiParameter? ReadParameter(JsonElement element) {
        var name     = GetString(element, "name");
        var location = GetString(element, "in");
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(location)) return null;

        if (!TryMapLocation(location!, out var apiLocation)) return null;

        var schema    = element.TryGetProperty("schema", out var schemaElement) ? ResolveSchema(schemaElement) : AnySchema();
        var parameter = new ApiParameter(name!, apiLocation, schema) {
            Required    = apiLocation == ApiParameterLocation.Path || GetBool(element, "required") == true,
            Description = GetString(element, "description"),
            Style       = GetString(element, "style"),
            Explode     = GetBool(element, "explode")
        };

        return parameter;
    }

    ApiRequestBody ReadRequestBody(JsonElement element) {
        var resolved = ResolveComponent(element, "requestBodies");
        var content  = ReadContent(resolved);
        return new ApiRequestBody(content, GetBool(resolved, "required") == true, GetString(resolved, "description"));
    }

    ApiResponse ReadResponse(string statusCode, JsonElement element) {
        var resolved = ResolveComponent(element, "responses");
        var content  = ReadContent(resolved);
        return new ApiResponse(statusCode, content, GetString(resolved, "description"));
    }

    List<ApiMediaType> ReadContent(JsonElement owner) {
        var result = new List<ApiMediaType>();

        if (!owner.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object) return result;

        foreach (var media in content.EnumerateObject()) {
            var schema = media.Value.TryGetProperty("schema", out var schemaElement) ? ResolveSchema(schemaElement) : null;
            result.Add(new ApiMediaType(media.Name, schema));
        }

        return result;
    }

    // --- $ref helpers --------------------------------------------------------------------------

    /// <summary>Resolves a component-level <c>$ref</c> (parameters/requestBodies/responses) or returns the element as-is.</summary>
    JsonElement ResolveComponent(JsonElement element, string componentKind) {
        if (!TryGetRef(element, out var refPath)) return element;

        if (TryResolvePointer(refPath!, out var target)) return target;

        throw new OpenApiParseException($"Unable to resolve {componentKind} reference '{refPath}'.");
    }

    static bool TryGetRef(JsonElement element, out string? refPath) {
        refPath = null;
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("$ref", out var refElement) && refElement.ValueKind == JsonValueKind.String) {
            refPath = refElement.GetString();
            return refPath != null;
        }

        return false;
    }

    bool TryResolvePointer(string refPath, out JsonElement target) {
        target = default;
        if (!refPath.StartsWith("#/", StringComparison.Ordinal)) return false; // external refs not supported

        var current = _root;

        foreach (var segment in refPath.Substring(2).Split('/')) {
            var decoded = segment.Replace("~1", "/").Replace("~0", "~");
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(decoded, out current)) return false;
        }

        target = current;
        return true;
    }

    static string SchemaNameFromRef(string value)
        => value.StartsWith("#/", StringComparison.Ordinal) ? value.Substring(value.LastIndexOf('/') + 1) : value;

    // --- Small JSON helpers --------------------------------------------------------------------

    static ApiSchema AnySchema() => new() { Kind = SchemaKind.Unknown };

    static string? GetSchemaType(JsonElement element) {
        if (!element.TryGetProperty("type", out var type)) return null;

        if (type.ValueKind == JsonValueKind.String) return type.GetString();

        // OpenAPI 3.1 type arrays: pick the first non-null type.
        if (type.ValueKind == JsonValueKind.Array) {
            foreach (var entry in type.EnumerateArray()) {
                if (entry.ValueKind == JsonValueKind.String && entry.GetString() != "null") return entry.GetString();
            }
        }

        return null;
    }

    static PrimitiveType MapPrimitive(string type)
        => type switch {
            "integer" => PrimitiveType.Integer,
            "number"  => PrimitiveType.Number,
            "boolean" => PrimitiveType.Boolean,
            _         => PrimitiveType.String
        };

    static bool TryMapMethod(string verb, out Method method) {
        switch (verb.ToLowerInvariant()) {
            case "get":     method = Method.Get;     return true;
            case "post":    method = Method.Post;    return true;
            case "put":     method = Method.Put;     return true;
            case "delete":  method = Method.Delete;  return true;
            case "head":    method = Method.Head;    return true;
            case "options": method = Method.Options; return true;
            case "patch":   method = Method.Patch;   return true;
            default:        method = Method.Get;     return false; // "trace" and non-method keys (parameters, summary, ...) are skipped
        }
    }

    static bool TryMapLocation(string location, out ApiParameterLocation result) {
        switch (location.ToLowerInvariant()) {
            case "path":   result = ApiParameterLocation.Path;   return true;
            case "query":  result = ApiParameterLocation.Query;  return true;
            case "header": result = ApiParameterLocation.Header; return true;
            case "cookie": result = ApiParameterLocation.Cookie; return true;
            default:        result = ApiParameterLocation.Query; return false;
        }
    }

    static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    static bool? GetBool(JsonElement element, string name) {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return null;

        return value.ValueKind switch {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _                   => null
        };
    }

    static double? GetDouble(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
}
