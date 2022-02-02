namespace laye;

internal sealed class SymbolTable
{
    private readonly Dictionary<string, Symbol> m_symbols = new();

    public SymbolTable? Parent { get; }

    private Symbol.Function? m_functionSymbol;
    public Symbol.Function? FunctionSymbol
    {
        get => m_functionSymbol ?? Parent?.FunctionSymbol;
        set => m_functionSymbol = value;
    }

    public SymbolTable(SymbolTable? parent = null)
    {
        Parent = parent;
    }

    public bool AddSymbol(Symbol symbol)
    {
        if (m_symbols.ContainsKey(symbol.Name))
            return false;

        m_symbols[symbol.Name] = symbol;
        return true;
    }

    public bool TryGetSymbol(string symbolName, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Symbol symbol) => m_symbols.TryGetValue(symbolName, out symbol);

    public Symbol? LookupSymbol(string symbolName)
    {
        SymbolTable? table = this;
        while (table is not null)
        {
            if (table.TryGetSymbol(symbolName, out var symbol))
                return symbol;

            table = table.Parent;
        }

        return null;
    }
}

internal abstract record class Symbol
{
    public string Name { get; init; }
    public SymbolType? Type { get; set; }

    protected Symbol(string name)
        : this(name, default)
    {
    }

    protected Symbol(string name, SymbolType? type)
    {
        Name = name;
        Type = type;
    }

    public sealed record class Binding : Symbol
    {
        public Binding(string name)
            : base(name)
        {
        }

        public Binding(string name, SymbolType? type)
            : base(name, type)
        {
        }
    }

    public sealed record class Function : Symbol<SymbolType.Function>
    {
        public Function(string name)
            : base(name)
        {
        }

        public Function(string name, SymbolType.Function? type)
            : base(name, type)
        {
        }
    }

    public sealed record class Struct : Symbol<SymbolType.Struct>
    {
        public Struct(string name)
            : base(name)
        {
        }

        public Struct(string name, SymbolType.Struct? type)
            : base(name, type)
        {
        }
    }
}

internal abstract record class Symbol<TSymbolType> : Symbol
    where TSymbolType : SymbolType
{
    public new TSymbolType? Type
    {
        get => base.Type as TSymbolType;
        set => base.Type = value;
    }

    protected Symbol(string name)
        : base(name, default)
    {
    }

    protected Symbol(string name, TSymbolType? type)
        : base(name, type)
    {
    }
}

internal abstract record class SymbolType(string Name)
{
    /// <summary>
    /// Represents a type that references a type parameter
    /// </summary>
    public sealed record class Param(string Name) : SymbolType(Name);

    public sealed record class Void() : SymbolType("void");
    public sealed record class Rune() : SymbolType("rune");

    public sealed record class UntypedBool() : SymbolType("untyped bool");
    public sealed record class Bool() : SymbolType("bool");
    public sealed record class SizedBool(uint BitCount) : SymbolType($"b{BitCount}");

    public sealed record class UntypedInteger(bool Signed) : SymbolType("untyped int");
    public sealed record class Integer(bool Signed) : SymbolType(Signed ? "int" : "uint");
    public sealed record class SizedInteger(bool Signed, uint BitCount) : SymbolType($"{(Signed ? "i" : "u")}{BitCount}");

    public sealed record class UntypedFloat() : SymbolType("untyped float");
    public sealed record class Float() : SymbolType("float");
    public sealed record class SizedFloat(uint BitCount) : SymbolType($"f{BitCount}");

    public sealed record class UntypedString() : SymbolType("untyped string");

    public sealed record class RawPtr() : SymbolType("rawptr");
    public sealed record class Array(SymbolType ElementType, uint ElementCount, bool ReadOnly = false) : SymbolType("array");
    public sealed record class Pointer(SymbolType ElementType, bool ReadOnly = false) : SymbolType("pointer");
    public sealed record class Buffer(SymbolType ElementType, bool ReadOnly = false) : SymbolType("buffer");
    public sealed record class Slice(SymbolType ElementType, bool ReadOnly = false) : SymbolType("slice");

    public sealed record class Function(string Name, TypeParam[] TypeParams, CallingConvention CallingConvention, SymbolType ReturnType, (SymbolType Type, string Name)[] Parameters, VarArgsKind VarArgs) : SymbolType($"function {Name}");
    public sealed record class Struct(string Name, TypeParam[] TypeParams, (SymbolType Type, string Name)[] Fields) : SymbolType(Name);
    public sealed record class Union(string Name, TypeParam[] TypeParams, (SymbolType Type, string Name)[] Variants) : SymbolType(Name);
    public sealed record class Enum(string Name, (string Name, uint Value)[] Variants) : SymbolType(Name);

    public sealed override string ToString() => Name;
}

internal abstract record class TypeParam(string Name)
{
    // e.g. vec2<T>
    public sealed record class TypeName(string Name) : TypeParam(Name);
    // e.g. vec<uint N>
    public sealed record class Constant(SymbolType ConstantType, string Name) : TypeParam(Name);
}
