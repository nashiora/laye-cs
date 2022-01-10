using System.Diagnostics;

namespace laye;

public abstract record class Token(SourceSpan SourceSpan) : IHasSourceSpan
{
    public sealed record class Integer(SourceSpan SourceSpan, ulong LiteralValue) : Token(SourceSpan);
    public sealed record class Float(SourceSpan SourceSpan, double LiteralValue) : Token(SourceSpan);
    public sealed record class String(SourceSpan SourceSpan, string LiteralValue) : Token(SourceSpan);
    public sealed record class Delimiter(SourceSpan SourceSpan, laye.Delimiter Kind) : Token(SourceSpan);

    public sealed record class Identifier(SourceSpan SourceSpan, string Image) : Token(SourceSpan);
    public sealed record class Operator(SourceSpan SourceSpan, string Image) : Token(SourceSpan);
    public sealed record class Keyword(SourceSpan SourceSpan, laye.Keyword Kind) : Token(SourceSpan);
}

internal static class TokenKeywordExt
{
    public static bool IsModifier(this Token.Keyword kw)
        => kw.Kind > Keyword._Modifier_Start_ && kw.Kind < Keyword._Modifier_End_;

    public static AstNode.Modifier ToModifierNode(this Token.Keyword kw)
    {
#if DEBUG
        Debug.Assert(kw.IsModifier(), "token is not a modifier token");
#endif

        if (kw.Kind > Keyword._Visibility_Modifier_Start_ && kw.Kind < Keyword._Visibility_Modifier_End_)
            return new AstNode.Visibility(kw);
        else if (kw.Kind > Keyword._CallingConvention_Modifier_Start_ && kw.Kind < Keyword._CallingConvention_Modifier_End_)
            return new AstNode.CallingConvention(kw);
        else if (kw.Kind > Keyword._FunctionHint_Modifier_Start_ && kw.Kind < Keyword._FunctionHint_Modifier_End_)
            return new AstNode.FunctionHint(kw);
        else if (kw.Kind > Keyword._Accessibility_Modifier_Start_ && kw.Kind < Keyword._Accessibility_Modifier_End_)
            return new AstNode.Accessibility(kw);

        throw new InvalidOperationException($"keyword with kind {kw.Kind} is not a valid modifier");
    }
}

public enum Delimiter
{
    Invalid = 0,

    OpenParen = '(',
    CloseParen = ')',

    OpenBracket = '[',
    CloseBracket = ']',

    OpenBrace = '{',
    CloseBrace = '}',

    Comma = ',',
    Dot = '.',
    Question = '?',

    Colon = ':',
    SemiColon = ';',

    PathSeparator = 256,
    FatArrow,
    QuestionDot,
}

public enum Keyword
{
    Invalid = 0,

    #region Type Names

    I8,
    I16,
    I32,
    I64,
    I128,
    Int,

    U8,
    U16,
    U32,
    U64,
    U128,
    UInt,

    CChar,
    CShort,
    CInt,
    CLong,
    CLongLong,

    CUChar,
    CUShort,
    CUInt,
    CULong,
    CULongLong,

    F16,
    F32,
    F64,
    Float,

    CFloat,
    CDouble,

    Void,
    Bool,
    RawPtr,
    
    NoReturn,
    Dynamic,

    Struct,
    Enum,

    VarArgs,

    #endregion

    #region Value-Like

    True,
    False,

    NullPtr,
    Nil,

    Context,
    Global,

    NoInit,

    #endregion

    #region Modifiers

    _Modifier_Start_,

    _Visibility_Modifier_Start_,

    Public,

    _Visibility_Modifier_End_,

    _CallingConvention_Modifier_Start_,

    NoContext,
    CDecl,
    FastCall,
    StdCall,

    _CallingConvention_Modifier_End_,

    _FunctionHint_Modifier_Start_,

    Intrinsic,
    Export,
    Extern,
    Inline,
    //Naked,

    _FunctionHint_Modifier_End_,

    _Accessibility_Modifier_Start_,

    Const, // const makes something actually a compile time constant
    ReadOnly, // readonly make something only readable (either only reading a binding, but not reassigning OR not allowing writes thru containers)
    //WriteOnly,

    _Accessibility_Modifier_End_,

    _Modifier_End_,

    #endregion

    #region Operators

    And,
    Or,
    Xor,
    Not,

    New,
    Cast,

    #endregion

    #region Control Flow

    If,
    Else,
    While,
    For,
    Switch,
    Case,
    Default,
    Return,
    Break,
    Continue,
    Yield,

    #endregion
}

internal static class KeywordExt
{
    public static string ToTokenString(this Keyword kw) => kw.ToString().ToLower();
}
