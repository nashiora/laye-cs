namespace laye;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModifierAllowedContextsAttribute : Attribute
{
    public readonly ModifierContext ModifierContext;

    public ModifierAllowedContextsAttribute(ModifierContext modifierContext)
    {
        ModifierContext = modifierContext;
    }
}

[Flags]
public enum ModifierContext
{
    None = 0,

    GlobalFunction = 1 << 1,
    LocalFunction = 1 << 2,
    GlobalBinding = 1 << 3,
    LocalBinding = 1 << 4,
    FunctionParameter = 1 << 5,
    Type = 1 << 6,
}
