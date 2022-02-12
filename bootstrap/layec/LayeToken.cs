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
    Remainder,

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

    String,
    
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

    Extern,

    Public,
    Internal,
    Private,

    NoContext,
    CDecl,
    FastCall,
    StdCall,

    Intrinsic,
    Export,
    Inline,
    Naked,

    Const, // const makes something actually a compile time constant
    ReadOnly, // readonly make something only readable (either only reading a binding, but not reassigning OR not allowing writes thru containers)
    WriteOnly,

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
