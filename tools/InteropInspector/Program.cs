// Lists types and member signatures of the BepInEx interop assemblies without loading them.
// Usage: dotnet run --project tools/InteropInspector -- <type regex> [--member <regex>] [--asm <assembly name>] [--bepinex <dir>]
// Example: dotnet run --project tools/InteropInspector -- "^GamepadController$" --member "Screen"
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

string? typePattern = null;
string? memberPattern = null;
string assemblyName = "ScriptAssembly";
string bepInEx = @"C:\Program Files (x86)\Steam\steamapps\common\Wingspan\BepInEx";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--member": memberPattern = args[++i]; break;
        case "--asm": assemblyName = args[++i]; break;
        case "--bepinex": bepInEx = args[++i]; break;
        default: typePattern = args[i]; break;
    }
}

if (typePattern == null)
{
    Console.Error.WriteLine("Usage: InteropInspector <type regex> [--member <regex>] [--asm <assembly>] [--bepinex <dir>]");
    return 1;
}

var paths = new List<string>();
paths.AddRange(Directory.GetFiles(Path.Combine(bepInEx, "interop"), "*.dll"));
paths.AddRange(Directory.GetFiles(Path.Combine(bepInEx, "core"), "*.dll"));
paths.AddRange(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));

using var context = new MetadataLoadContext(new PathAssemblyResolver(paths));
Assembly assembly = context.LoadFromAssemblyPath(Path.Combine(bepInEx, "interop", assemblyName + ".dll"));
var typeRegex = new Regex(typePattern);
var memberRegex = memberPattern == null ? null : new Regex(memberPattern, RegexOptions.IgnoreCase);
const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

Type[] types;
try { types = assembly.GetTypes(); }
catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }

foreach (Type type in types.Where(t => typeRegex.IsMatch(t.FullName ?? t.Name)).OrderBy(t => t.FullName))
{
    Console.WriteLine($"{type.FullName} : {Safe(() => Name(type.BaseType))}");
    foreach (MemberInfo member in Safe(() => type.GetMembers(Flags), Array.Empty<MemberInfo>()))
    {
        if (member is MethodInfo { IsSpecialName: true } or ConstructorInfo)
            continue;
        string line = Safe(() => Describe(member));
        if (memberRegex == null || memberRegex.IsMatch(line))
            Console.WriteLine("  " + line);
    }
}

return 0;

static string Describe(MemberInfo member) => member switch
{
    MethodInfo m => $"{(m.IsStatic ? "static " : "")}{Name(m.ReturnType)} {m.Name}" +
                    (m.IsGenericMethodDefinition ? $"<{string.Join(", ", m.GetGenericArguments().Select(Name))}>" : "") +
                    $"({string.Join(", ", m.GetParameters().Select(p => $"{Name(p.ParameterType)} {p.Name}"))})",
    PropertyInfo p => $"{((p.GetMethod ?? p.SetMethod)?.IsStatic == true ? "static " : "")}{Name(p.PropertyType)} {p.Name} {{{(p.CanRead ? " get;" : "")}{(p.CanWrite ? " set;" : "")} }}",
    FieldInfo f => $"{(f.IsStatic ? "static " : "")}field {Name(f.FieldType)} {f.Name}",
    EventInfo e => $"event {Name(e.EventHandlerType)} {e.Name}",
    Type t => $"nested {t.Name}",
    _ => member.ToString() ?? member.Name,
};

static string Name(Type? type)
{
    if (type == null) return "?";
    if (!type.IsGenericType) return type.Name;
    string baseName = type.Name.Split('`')[0];
    return $"{baseName}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";
}

static T Safe<T>(Func<T> func, T fallback = default!)
{
    try { return func(); }
    catch (Exception e) { return fallback is null && typeof(T) == typeof(string) ? (T)(object)$"<{e.GetType().Name}>" : fallback; }
}
