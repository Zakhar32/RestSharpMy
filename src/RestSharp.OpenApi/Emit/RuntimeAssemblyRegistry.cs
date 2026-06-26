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

using System.Collections.Concurrent;
using System.Reflection;

namespace RestSharp.OpenApi.Emit;

/// <summary>
/// Tracks the dynamic assemblies emitted by <see cref="RuntimeTypeFactory"/> and resolves them by
/// name. This is required because attributes we emit (notably <c>[JsonDerivedType(typeof(Derived))]</c>)
/// store a type reference that the runtime later re-resolves through <c>Assembly.Load</c> by the
/// assembly's display name. A <see cref="System.Reflection.Emit.AssemblyBuilderAccess.Run"/> assembly
/// is not loadable from disk, so without this resolver System.Text.Json throws
/// <see cref="System.IO.FileNotFoundException"/> when it reads those attributes.
/// </summary>
static class RuntimeAssemblyRegistry {
    static readonly ConcurrentDictionary<string, Assembly> Assemblies = new(StringComparer.Ordinal);
    static          int                                    _hooked;

    public static void Register(Assembly assembly) {
        EnsureResolverHooked();
        var name = assembly.GetName().Name;
        if (name != null) Assemblies[name] = assembly;
    }

    static void EnsureResolverHooked() {
        if (Interlocked.Exchange(ref _hooked, 1) != 0) return;
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    static Assembly? Resolve(object? sender, ResolveEventArgs args) {
        var name = new AssemblyName(args.Name).Name;
        return name != null && Assemblies.TryGetValue(name, out var assembly) ? assembly : null;
    }
}
