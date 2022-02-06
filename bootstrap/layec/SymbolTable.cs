using System.Text;

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

    public sealed record class UntypedBool() : SymbolType("<untyped bool>");
    public sealed record class Bool() : SymbolType("bool");
    public sealed record class SizedBool(uint BitCount) : SymbolType($"b{BitCount}");

    public sealed record class UntypedInteger(bool Signed) : SymbolType("<untyped int>");
    public sealed record class Integer(bool Signed) : SymbolType(Signed ? "int" : "uint");
    public sealed record class SizedInteger(bool Signed, uint BitCount) : SymbolType($"{(Signed ? "i" : "u")}{BitCount}");

    public sealed record class UntypedFloat() : SymbolType("<untyped float>");
    public sealed record class Float() : SymbolType("float");
    public sealed record class SizedFloat(uint BitCount) : SymbolType($"f{BitCount}");

    public sealed record class UntypedString() : SymbolType("<untyped string>");
    public sealed record class String() : SymbolType("string");

    public sealed record class RawPtr() : SymbolType("rawptr");
    public sealed record class Array(SymbolType ElementType, uint ElementCount, bool ReadOnly = false) : SymbolType($"{ElementType}{(ReadOnly ? " readonly" : "")}[{ElementCount}]");
    public sealed record class Pointer(SymbolType ElementType, AccessKind Access = AccessKind.ReadWrite) : SymbolType($"{ElementType}{(Access != AccessKind.ReadWrite ? $" {Access.ToString().ToLower()}" : "")}*");
    public sealed record class Buffer(SymbolType ElementType, AccessKind Access = AccessKind.ReadWrite) : SymbolType($"{ElementType}{(Access != AccessKind.ReadWrite ? $" {Access.ToString().ToLower()}" : "")}[*]");
    public sealed record class Slice(SymbolType ElementType, AccessKind Access = AccessKind.ReadWrite) : SymbolType($"{ElementType}{(Access != AccessKind.ReadWrite ? $" {Access.ToString().ToLower()}" : "")}[]");

    public sealed record class Function(string Name, CallingConvention CallingConvention, SymbolType ReturnType, (SymbolType Type, string Name)[] Parameters, VarArgsKind VarArgs)
        : SymbolType(FunctionTypeToString(Name, CallingConvention, ReturnType, Parameters.Select(p => $"{p.Type} {p.Name}").ToArray(), VarArgs));
    public sealed record class FunctionPointer(CallingConvention CallingConvention, SymbolType ReturnType, SymbolType[] ParameterTypes, VarArgsKind VarArgs)
        : SymbolType(FunctionTypeToString(null, CallingConvention, ReturnType, ParameterTypes.Select(p => p.Name).ToArray(), VarArgs));
    public sealed record class Struct(string Name, (SymbolType Type, string Name)[] Fields) : SymbolType(Name);
    public sealed record class Union(string Name, (SymbolType Type, string Name)[] Variants) : SymbolType(Name);
    public sealed record class Enum(string Name, (string Name, uint Value)[] Variants) : SymbolType(Name);

    public sealed override string ToString() => Name;

    private static string FunctionTypeToString(string? name, CallingConvention callingConvention, SymbolType returnType, string[] parameters, VarArgsKind varArgs)
    {
        var builder = new StringBuilder();
        builder.Append(returnType);

        if (callingConvention != CallingConvention.Laye)
        {
            builder.Append(' ');
            builder.Append(callingConvention.ToString().ToLower());
        }

        if (name is not null)
        {
            builder.Append(' ');
            builder.Append(name);
        }

        builder.Append('(');

        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            if (i == parameters.Length - 1 && varArgs == VarArgsKind.Laye)
                builder.Append("varargs ");

            builder.Append(parameters[i]);
        }

        if (varArgs == VarArgsKind.C)
        {
            if (parameters.Length > 0)
                builder.Append(", ");
            builder.Append("varargs");
        }

        builder.Append(')');

        return builder.ToString();
    }
}
