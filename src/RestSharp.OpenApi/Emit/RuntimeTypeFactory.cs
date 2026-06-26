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

using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;
using RestSharp.OpenApi.Model;

namespace RestSharp.OpenApi.Emit;

/// <summary>
/// Materialises CLR types at runtime from the semantic model's named object schemas, using
/// <see cref="System.Reflection.Emit"/>. Named object schemas become public classes; composition
/// (<c>allOf</c>) becomes inheritance; a base schema with a discriminator becomes a polymorphic base
/// with <c>[JsonPolymorphic]</c>/<c>[JsonDerivedType]</c> attributes so System.Text.Json can round-trip
/// the hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// Generation is performed once for the whole document (a single dynamic module per factory) and the
/// resulting <see cref="Type"/>s are cached. The factory is thread-safe: the one-time generation is
/// guarded by a lock and the resulting map is immutable afterwards.
/// </para>
/// <para>
/// Documented limitations: inline (anonymous) object schemas and <c>oneOf</c>/<c>anyOf</c> composite
/// schemas are surfaced as <see cref="object"/> (System.Text.Json yields a <c>JsonElement</c>). Only
/// named component schemas become concrete classes. If emitting the System.Text.Json polymorphism
/// attributes fails on the host runtime, generation transparently falls back to emitting the
/// discriminator as a plain property.
/// </para>
/// <para>
/// Polymorphism uses System.Text.Json's discriminator support, which requires the discriminator to be
/// the <b>first</b> property of the JSON object on .NET 8. On .NET 9+ callers can lift that with
/// <c>JsonSerializerOptions.AllowOutOfOrderMetadataProperties</c>. If your server emits the
/// discriminator out of order on .NET 8, deserialize into the concrete type rather than the base.
/// </para>
/// </remarks>
sealed class RuntimeTypeFactory {
    static readonly ConstructorInfo JsonPropertyNameCtor =
        typeof(JsonPropertyNameAttribute).GetConstructor(new[] { typeof(string) })!;

    static readonly ConstructorInfo? JsonPolymorphicCtor =
        typeof(JsonPolymorphicAttribute).GetConstructor(Type.EmptyTypes);

    static readonly PropertyInfo? JsonPolymorphicDiscriminatorProperty =
        typeof(JsonPolymorphicAttribute).GetProperty(nameof(JsonPolymorphicAttribute.TypeDiscriminatorPropertyName));

    static readonly ConstructorInfo? JsonDerivedTypeCtor =
        typeof(JsonDerivedTypeAttribute).GetConstructor(new[] { typeof(Type), typeof(string) });

    readonly OpenApiDocumentModel _model;
    readonly string               _namespace;
    readonly object               _gate = new();

    Dictionary<string, Type>? _types;

    public RuntimeTypeFactory(OpenApiDocumentModel model, string @namespace) {
        _model     = model;
        _namespace = string.IsNullOrWhiteSpace(@namespace) ? "RestSharp.OpenApi.Generated" : @namespace;
    }

    /// <summary>All generated types, keyed by schema (component) name.</summary>
    public IReadOnlyDictionary<string, Type> GeneratedTypes {
        get {
            EnsureGenerated();
            return _types!;
        }
    }

    /// <summary>Returns the generated type for the named schema, or null when there isn't one.</summary>
    public Type? TryGetType(string schemaName) {
        EnsureGenerated();
        return _types!.TryGetValue(schemaName, out var type) ? type : null;
    }

    /// <summary>
    /// Resolves any schema to a usable CLR type: generated class for named objects, <see cref="List{T}"/>
    /// for arrays, <see cref="Dictionary{TKey,TValue}"/> for maps, BCL types for primitives, and
    /// <see cref="object"/> for inline/anonymous and composite schemas.
    /// </summary>
    public Type ResolveType(ApiSchema schema, bool nullableContext = true) {
        EnsureGenerated();
        return ResolveTypeCore(schema, nullableContext);
    }

    Type ResolveTypeCore(ApiSchema schema, bool nullableContext) {
        switch (schema.Kind) {
            case SchemaKind.Primitive:
            case SchemaKind.Enum:
                return ClrTypeMapper.MapPrimitive(schema, nullableContext);
            case SchemaKind.Array:
                return typeof(List<>).MakeGenericType(schema.Items != null ? ResolveTypeCore(schema.Items, true) : typeof(object));
            case SchemaKind.Map:
                return typeof(Dictionary<,>).MakeGenericType(
                    typeof(string),
                    schema.AdditionalProperties != null ? ResolveTypeCore(schema.AdditionalProperties, true) : typeof(object)
                );
            case SchemaKind.Object when schema.Name != null && _types!.TryGetValue(schema.Name, out var named):
                return named;
            default:
                return typeof(object);
        }
    }

    void EnsureGenerated() {
        if (_types != null) return;

        lock (_gate) {
            if (_types != null) return;

            try {
                _types = Generate(emitPolymorphism: true);
            }
            catch (Exception ex) when (ex is not OpenApiTypeGenerationException) {
                // Polymorphism attribute emission is the fragile part; retry without it before giving up.
                _types = Generate(emitPolymorphism: false);
            }
        }
    }

    Dictionary<string, Type> Generate(bool emitPolymorphism) {
        var assemblyName = new AssemblyName($"RestSharp.OpenApi.Dynamic.{Guid.NewGuid():N}");
        var assembly     = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module       = assembly.DefineDynamicModule(assemblyName.Name!);

        // Register so the emitted [JsonDerivedType] attributes can be resolved by name at read time.
        RuntimeAssemblyRegistry.Register(assembly);

        // Only named object schemas become classes. Order base-first so parents exist before children.
        var objectSchemas = _model.Schemas.Values
            .Where(s => s.Kind == SchemaKind.Object && s.Name != null)
            .ToList();

        var ordered  = OrderByInheritance(objectSchemas);
        var builders = new Dictionary<string, TypeBuilder>(StringComparer.Ordinal);
        var ctors    = new Dictionary<string, ConstructorBuilder>(StringComparer.Ordinal);

        // Phase 1: declare all type builders (so cross-references resolve).
        foreach (var schema in ordered) {
            var parent = schema.BaseSchema?.Name != null && builders.TryGetValue(schema.BaseSchema.Name, out var baseBuilder)
                ? baseBuilder
                : typeof(object);

            var typeBuilder = module.DefineType(
                $"{_namespace}.{ClrTypeMapper.ToPascalCase(schema.Name!)}",
                TypeAttributes.Public | TypeAttributes.Class,
                (Type)parent
            );

            builders[schema.Name!] = typeBuilder;
        }

        // Phase 2: constructors (chaining to base) and properties.
        foreach (var schema in ordered) {
            var typeBuilder = builders[schema.Name!];
            var baseCtor    = schema.BaseSchema?.Name != null && ctors.TryGetValue(schema.BaseSchema.Name, out var inherited)
                ? (ConstructorInfo)inherited
                : ObjectCtor;

            ctors[schema.Name!] = DefineDefaultCtor(typeBuilder, baseCtor);
        }

        // When System.Text.Json owns the discriminator (polymorphic base with derived types), the
        // discriminator property must NOT also be a declared CLR property or STJ rejects the conflict.
        var discriminatorToSkip = new Dictionary<string, string>(StringComparer.Ordinal);

        if (emitPolymorphism) {
            foreach (var schema in ordered) {
                if (schema.Discriminator != null && schema.Name != null && FindDerivedTypes(schema, builders).Count > 0)
                    discriminatorToSkip[schema.Name] = schema.Discriminator.PropertyName;
            }
        }

        foreach (var schema in ordered) {
            discriminatorToSkip.TryGetValue(schema.Name!, out var skip);
            EmitProperties(builders[schema.Name!], schema, builders, skip);
        }

        if (emitPolymorphism) EmitPolymorphismAttributes(ordered, builders);

        // Phase 3: bake. Base types must be created before derived types.
        var created = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var schema in ordered) created[schema.Name!] = builders[schema.Name!].CreateTypeInfo()!.AsType();

        return created;
    }

    /// <summary>Topological order so a schema's <see cref="ApiSchema.BaseSchema"/> always precedes it.</summary>
    static List<ApiSchema> OrderByInheritance(List<ApiSchema> schemas) {
        var byName  = schemas.Where(s => s.Name != null).ToDictionary(s => s.Name!, s => s, StringComparer.Ordinal);
        var ordered = new List<ApiSchema>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ApiSchema schema) {
            if (schema.Name == null || !visited.Add(schema.Name)) return;
            if (schema.BaseSchema?.Name != null && byName.TryGetValue(schema.BaseSchema.Name, out var baseSchema)) Visit(baseSchema);
            ordered.Add(schema);
        }

        foreach (var schema in schemas) Visit(schema);
        return ordered;
    }

    void EmitProperties(TypeBuilder typeBuilder, ApiSchema schema, Dictionary<string, TypeBuilder> builders, string? discriminatorToSkip) {
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        // Reserve names already declared by base types so we don't shadow them.
        for (var baseSchema = schema.BaseSchema; baseSchema != null; baseSchema = baseSchema.BaseSchema) {
            foreach (var property in baseSchema.Properties) usedNames.Add(ClrTypeMapper.ToPascalCase(property.Name));
        }

        foreach (var property in schema.Properties) {
            // System.Text.Json manages the discriminator property itself for polymorphic bases.
            if (discriminatorToSkip != null && property.Name == discriminatorToSkip) continue;

            // Skip properties inherited from the base (declared on the base type already).
            if (IsDeclaredByBase(schema.BaseSchema, property.Name)) continue;

            var clrName = UniqueName(ClrTypeMapper.ToPascalCase(property.Name), typeBuilder.Name, usedNames);
            var clrType = ResolvePropertyType(property.Schema, property.Required, builders);
            EmitAutoProperty(typeBuilder, clrName, property.Name, clrType);
        }
    }

    static bool IsDeclaredByBase(ApiSchema? baseSchema, string wireName) {
        for (var current = baseSchema; current != null; current = current.BaseSchema) {
            if (current.Properties.Any(p => p.Name == wireName)) return true;
        }

        return false;
    }

    /// <summary>Resolves a property type, preferring the type builder for named objects still being defined.</summary>
    Type ResolvePropertyType(ApiSchema schema, bool required, Dictionary<string, TypeBuilder> builders) {
        switch (schema.Kind) {
            case SchemaKind.Primitive:
            case SchemaKind.Enum:
                return ClrTypeMapper.MapPrimitive(schema, nullableContext: !required);
            case SchemaKind.Array:
                return typeof(List<>).MakeGenericType(schema.Items != null ? ResolvePropertyType(schema.Items, true, builders) : typeof(object));
            case SchemaKind.Map:
                return typeof(Dictionary<,>).MakeGenericType(
                    typeof(string),
                    schema.AdditionalProperties != null ? ResolvePropertyType(schema.AdditionalProperties, true, builders) : typeof(object)
                );
            case SchemaKind.Object when schema.Name != null && builders.TryGetValue(schema.Name, out var builder):
                return builder;
            default:
                return typeof(object);
        }
    }

    void EmitAutoProperty(TypeBuilder typeBuilder, string clrName, string wireName, Type type) {
        var field = typeBuilder.DefineField($"_{clrName}", type, FieldAttributes.Private);

        var getter = typeBuilder.DefineMethod(
            $"get_{clrName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            type,
            Type.EmptyTypes
        );
        var getIl = getter.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, field);
        getIl.Emit(OpCodes.Ret);

        var setter = typeBuilder.DefineMethod(
            $"set_{clrName}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null,
            new[] { type }
        );
        var setIl = setter.GetILGenerator();
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, field);
        setIl.Emit(OpCodes.Ret);

        var property = typeBuilder.DefineProperty(clrName, PropertyAttributes.None, type, null);
        property.SetGetMethod(getter);
        property.SetSetMethod(setter);

        // [JsonPropertyName("wireName")] guarantees correct mapping regardless of casing policy.
        property.SetCustomAttribute(new CustomAttributeBuilder(JsonPropertyNameCtor, new object[] { wireName }));
    }

    void EmitPolymorphismAttributes(List<ApiSchema> ordered, Dictionary<string, TypeBuilder> builders) {
        if (JsonPolymorphicCtor == null || JsonDerivedTypeCtor == null || JsonPolymorphicDiscriminatorProperty == null) return;

        foreach (var schema in ordered) {
            if (schema.Discriminator == null || schema.Name == null) continue;

            var baseBuilder = builders[schema.Name];
            var derived     = FindDerivedTypes(schema, builders);
            if (derived.Count == 0) continue;

            baseBuilder.SetCustomAttribute(
                new CustomAttributeBuilder(
                    JsonPolymorphicCtor,
                    Array.Empty<object>(),
                    new[] { JsonPolymorphicDiscriminatorProperty },
                    new object[] { schema.Discriminator.PropertyName }
                )
            );

            foreach (var (discriminatorValue, derivedBuilder) in derived) {
                baseBuilder.SetCustomAttribute(
                    new CustomAttributeBuilder(JsonDerivedTypeCtor, new object[] { derivedBuilder, discriminatorValue })
                );
            }
        }
    }

    /// <summary>
    /// Finds the concrete derived types of a polymorphic base, together with the discriminator value
    /// for each. Values come from the explicit discriminator mapping, defaulting to the schema name.
    /// </summary>
    List<(string Value, TypeBuilder Builder)> FindDerivedTypes(ApiSchema baseSchema, Dictionary<string, TypeBuilder> builders) {
        var result    = new List<(string, TypeBuilder)>();
        var byName     = new Dictionary<string, string>(StringComparer.Ordinal); // schemaName -> discriminator value
        var discriminator = baseSchema.Discriminator!;

        foreach (var kvp in discriminator.Mapping) byName[kvp.Value] = kvp.Key; // value -> name => name -> value

        foreach (var candidate in _model.Schemas.Values) {
            if (candidate.Name == null || candidate.Name == baseSchema.Name) continue;
            if (!InheritsFrom(candidate, baseSchema)) continue;
            if (!builders.TryGetValue(candidate.Name, out var builder)) continue;

            var value = byName.TryGetValue(candidate.Name, out var mapped) ? mapped : candidate.Name;
            result.Add((value, builder));
        }

        return result;
    }

    static bool InheritsFrom(ApiSchema schema, ApiSchema ancestor) {
        for (var current = schema.BaseSchema; current != null; current = current.BaseSchema) {
            if (ReferenceEquals(current, ancestor) || current.Name == ancestor.Name) return true;
        }

        return false;
    }

    static ConstructorBuilder DefineDefaultCtor(TypeBuilder typeBuilder, ConstructorInfo baseCtor) {
        var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il   = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
        return ctor;
    }

    static string UniqueName(string candidate, string typeName, HashSet<string> used) {
        // A member may not share its simple name with the declaring type in C#; avoid it for tooling friendliness.
        var simpleTypeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
        var name           = candidate == simpleTypeName ? candidate + "Value" : candidate;

        var unique = name;
        var index  = 1;
        while (!used.Add(unique)) unique = $"{name}{index++}";

        return unique;
    }

    static readonly ConstructorInfo ObjectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;
}
