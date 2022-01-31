using System.Diagnostics;

namespace laye;

internal abstract record class LayeToken(SourceSpan SourceSpan) : IHasSourceSpan
{
    public sealed record class Integer(SourceSpan SourceSpan, ulong LiteralValue) : LayeToken(SourceSpan);
    public sealed record class Float(SourceSpan SourceSpan, double LiteralValue) : LayeToken(SourceSpan);
    public sealed record class String(SourceSpan SourceSpan, string LiteralValue) : LayeToken(SourceSpan);
    public sealed record class Character(SourceSpan SourceSpan, char LiteralValue) : LayeToken(SourceSpan);
    public sealed record class Delimiter(SourceSpan SourceSpan, laye.Delimiter Kind) : LayeToken(SourceSpan);

    public sealed record class Identifier(SourceSpan SourceSpan, string Image) : LayeToken(SourceSpan);

    public sealed record class Operator(SourceSpan SourceSpan, laye.Operator Kind) : LayeToken(SourceSpan);
    public sealed record class Keyword(SourceSpan SourceSpan, laye.Keyword Kind, uint SizeData = 0) : LayeToken(SourceSpan);
}

internal static class TokenKeywordExt
{
    public static bool IsModifier(this LayeToken.Keyword kw)
        => kw.Kind > Keyword._Modifier_Start_ && kw.Kind < Keyword._Modifier_End_;

    public static LayeAst.Modifier ToModifierNode(this LayeToken.Keyword kw)
    {
#if DEBUG
        Debug.Assert(kw.IsModifier(), "token is not a modifier token");
#endif

        if (kw.Kind > Keyword._Visibility_Modifier_Start_ && kw.Kind < Keyword._Visibility_Modifier_End_)
            return new LayeAst.Visibility(kw);
        else if (kw.Kind > Keyword._CallingConvention_Modifier_Start_ && kw.Kind < Keyword._CallingConvention_Modifier_End_)
            return new LayeAst.CallingConvention(kw);
        else if (kw.Kind > Keyword._FunctionHint_Modifier_Start_ && kw.Kind < Keyword._FunctionHint_Modifier_End_)
            return new LayeAst.FunctionHint(kw);
        else if (kw.Kind > Keyword._Accessibility_Modifier_Start_ && kw.Kind < Keyword._Accessibility_Modifier_End_)
            return new LayeAst.Accessibility(kw);

        throw new InvalidOperationException($"keyword with kind {kw.Kind} is not a valid modifier");
    }
}

internal enum Delimiter
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

internal enum Operator
{
    Invalid = 0,

    Assign,

    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,

    BitAnd,
    BitOr,
    BitXor,
    BitNot,

    LeftShift,
    RightShift,

    CompareEqual,
    CompareNotEqual,
    CompareGreaterThan,
    CompareGreaterThanOrEqual,
    CompareLessThan,
    CompareLessThanOrEqual,
}

internal enum Keyword
{
    Invalid = 0,

    #region Type Names

    Int,
    UInt,
    Bool,
    SizedInt,
    SizedUInt,
    SizedBool,

    SizedFloat,
    Float,

    Void,
    Rune,

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
    Naked,

    _FunctionHint_Modifier_End_,

    _Accessibility_Modifier_Start_,

    Const, // const makes something actually a compile time constant
    ReadOnly, // readonly make something only readable (either only reading a binding, but not reassigning OR not allowing writes thru containers)
    WriteOnly,

    _Accessibility_Modifier_End_,

    _Modifier_End_,

    #endregion

    #region Operators

    And,
    Or,
    Xor,
    Not,

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
