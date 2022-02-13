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

internal static class SymbolTypes
{
    public static readonly SymbolType.Void Void = new();
    public static readonly SymbolType.Rune Rune = new();

    //public static readonly SymbolType.UntypedBool UntypedBool = new();
    public static readonly SymbolType.Bool Bool = new();

#if false
    public static readonly SymbolType.SizedBool B8 = new(8);
    public static readonly SymbolType.SizedBool B32 = new(32);
#endif

    public static readonly SymbolType.UntypedInteger UntypedInt = new(true);
    public static readonly SymbolType.UntypedInteger UntypedUInt = new(false);

    public static readonly SymbolType.Integer Int = new(true);
    public static readonly SymbolType.Integer UInt = new(false);

    public static readonly SymbolType.SizedInteger I8 = new(true, 8);
    public static readonly SymbolType.SizedInteger I16 = new(true, 16);
    public static readonly SymbolType.SizedInteger I32 = new(true, 32);
    public static readonly SymbolType.SizedInteger I64 = new(true, 64);

    public static readonly SymbolType.SizedInteger U8 = new(false, 8);
    public static readonly SymbolType.SizedInteger U16 = new(false, 16);
    public static readonly SymbolType.SizedInteger U32 = new(false, 32);
    public static readonly SymbolType.SizedInteger U64 = new(false, 64);

    public static readonly SymbolType.UntypedFloat UntypedFloat = new();
    public static readonly SymbolType.Float Float = new();

#if false
    public static readonly SymbolType.SizedFloat F32 = new(32);
    public static readonly SymbolType.SizedFloat F64 = new(64);
#endif

    public static readonly SymbolType.UntypedString UntypedString = new();
    public static readonly SymbolType.String String = new();

    public static readonly SymbolType.RawPtr RawPtr = new();

    public static readonly SymbolType.Buffer U8Buffer = new(U8, AccessKind.ReadWrite);
    public static readonly SymbolType.Buffer ReadOnlyU8Buffer = new(U8, AccessKind.ReadOnly);

    public static readonly SymbolType.Slice U8Slice = new(U8, AccessKind.ReadWrite);
    public static readonly SymbolType.Slice ReadOnlyU8Slice = new(U8, AccessKind.ReadOnly);
}

internal abstract record class SymbolType(string Name)
{
    /// <summary>
    /// Represents a type that references a type parameter
    /// </summary>
    public sealed record class Param(string Name) : SymbolType(Name);

    public sealed record class Void() : SymbolType("void");
    public sealed record class Rune() : SymbolType("rune");

    //public sealed record class UntypedBool() : SymbolType("<untyped bool>");
    public sealed record class Bool() : SymbolType("bool");
    //public sealed record class SizedBool(uint BitCount) : SymbolType($"b{BitCount}");

    public sealed record class UntypedInteger(bool Signed) : SymbolType($"<untyped {(Signed ? "" : "u")}int>");
    public sealed record class Integer(bool Signed) : SymbolType(Signed ? "int" : "uint");
    public sealed record class SizedInteger(bool Signed, uint BitCount) : SymbolType($"{(Signed ? "i" : "u")}{BitCount}");

    public sealed record class UntypedFloat() : SymbolType("<untyped float>");
    public sealed record class Float() : SymbolType("float");
#if false
    public sealed record class SizedFloat(uint BitCount) : SymbolType($"f{BitCount}");
#endif

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

internal static class SymbolTypeExtensions
{
    public static bool IsSigned(this SymbolType type)
    {
        switch (type)
        {
            case SymbolType.UntypedInteger u: return u.Signed;
            case SymbolType.Integer i: return i.Signed;
            case SymbolType.SizedInteger s: return s.Signed;

            case SymbolType.UntypedFloat:
            case SymbolType.Float:
                //case SymbolType.SizedFloat:
                return true;

            default: return false;
        }
    }

    public static bool IsNumeric(this SymbolType type)
    {
        switch (type)
        {
            case SymbolType.UntypedInteger:
            case SymbolType.Integer:
            case SymbolType.SizedInteger:
            case SymbolType.UntypedFloat:
            case SymbolType.Float:
            //case SymbolType.SizedFloat:
                return true;

            default: return false;
        }
    }

    public static bool IsInteger(this SymbolType type)
    {
        switch (type)
        {
            case SymbolType.UntypedInteger:
            case SymbolType.Integer:
            case SymbolType.SizedInteger:
                return true;

            default: return false;
        }
    }

    public static bool IsFloat(this SymbolType type)
    {
        switch (type)
        {
            case SymbolType.UntypedFloat:
            case SymbolType.Float:
                //case SymbolType.SizedFloat:
                return true;

            default: return false;
        }
    }
}
