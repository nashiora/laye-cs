namespace laye;

public sealed class SymbolTable
{
    private readonly Dictionary<string, List<Symbol>> m_symbols = new();

    public void AddSymbol(Symbol symbol)
    {
        if (!m_symbols.TryGetValue(symbol.Name, out var symbols))
            m_symbols[symbol.Name] = symbols = new();

        symbols.Add(symbol);
    }
}

public abstract record class Symbol(string Name, SymbolType Type)
{
    public sealed record class Binding(string Name, SymbolType Type) : Symbol(Name, Type);
    public sealed record class Function(string Name, SymbolType.Function FunctionType) : Symbol(Name, FunctionType);
    public sealed record class Struct(string Name, SymbolType.Struct StructType) : Symbol(Name, StructType);
}

public abstract record class SymbolType(string Name)
{
    /// <summary>
    /// Represents a type that references a type parameter
    /// </summary>
    /// <param name="Name"></param>
    public sealed record class Param(string Name) : SymbolType(Name);

    public sealed record class Void() : SymbolType("void");
    public sealed record class Bool() : SymbolType("bool");

    public sealed record class Integer(bool Signed) : SymbolType(Signed ? "int" : "uint");
    public sealed record class SizedInteger(bool Signed, int BitCount) : SymbolType($"{(Signed ? "i" : "u")}{BitCount}");

    public sealed record class Float() : SymbolType("float");
    public sealed record class SizedFloat(int BitCount) : SymbolType($"f{BitCount}");

    public sealed record class Function(string Name, TypeParam[] TypeParams, CallingConvention CallingConvention, SymbolType ReturnType, SymbolType[] ParameterTypes, VarArgsKind VarArgs) : SymbolType($"function {Name}");
    public sealed record class Struct(string Name, TypeParam[] TypeParams, (SymbolType Type, string Name)[] Fields) : SymbolType(Name);
    public sealed record class Union(string Name, TypeParam[] TypeParams, (SymbolType Type, string Name)[] Variants) : SymbolType(Name);
    public sealed record class Enum(string Name, (string Name, uint Value)[] Variants) : SymbolType(Name);
}

public abstract record class TypeParam(string Name)
{
    // e.g. vec2<T>
    public sealed record class TypeName(string Name) : TypeParam(Name);
    // e.g. vec<uint N>
    public sealed record class Constant(SymbolType ConstantType, string Name) : TypeParam(Name);
}
