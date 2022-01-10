using System.Reflection;

namespace laye;

public abstract record class AstNode(SourceSpan SourceSpan) : IHasSourceSpan
{
    #region Shared Data

    public sealed record class BindingData(Modifier[] Modifiers, Type BindingType, Token.Identifier BindingName)
        : AstNode(new SourceSpan((Modifiers.Length == 0 ? (IHasSourceSpan)BindingType : Modifiers[0]).SourceSpan.StartLocation, BindingName.SourceSpan.EndLocation));

    public sealed record class ParamData(BindingData Binding, Token.Operator? AssignOperator, Expr? DefaultValue)
        : AstNode(new SourceSpan(Binding.SourceSpan.StartLocation, (DefaultValue ?? (IHasSourceSpan)Binding).SourceSpan.EndLocation));

    public abstract record class PathPart(SourceSpan SourceSpan);
    public sealed record class EmptyPathPart(SourceLocation Location) : PathPart(new SourceSpan(Location, Location));
    public sealed record class GlobalPathPart(Token.Keyword GlobalKeyword) : PathPart(GlobalKeyword.SourceSpan);
    public sealed record class NamePathPart(Token.Identifier Name) : PathPart(Name.SourceSpan);
    public sealed record class JoinedPath(PathPart BasePath, Token.Delimiter PathSeparator, NamePathPart LookupPart)
        : PathPart(new SourceSpan(BasePath.SourceSpan.StartLocation, LookupPart.SourceSpan.EndLocation));

    #endregion

    #region Modifiers

    public abstract record class Modifier(SourceSpan SourceSpan) : AstNode(SourceSpan);

    [ModifierAllowedContexts(ModifierContext.GlobalFunction | ModifierContext.GlobalBinding)]
    public sealed record class Visibility(Token.Keyword VisibilityKeyword) : Modifier(VisibilityKeyword.SourceSpan);

    [ModifierAllowedContexts(ModifierContext.GlobalFunction | ModifierContext.LocalFunction)]
    public sealed record class CallingConvention(Token.Keyword ConventionKeyword) : Modifier(ConventionKeyword.SourceSpan);

    [ModifierAllowedContexts(ModifierContext.GlobalFunction | ModifierContext.LocalFunction)]
    public sealed record class FunctionHint(Token.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);

    [ModifierAllowedContexts(ModifierContext.GlobalBinding | ModifierContext.LocalBinding | ModifierContext.Type)]
    public sealed record class Accessibility(Token.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);

    #endregion

    #region Types

    public abstract record class Type(SourceSpan SourceSpan) : AstNode(SourceSpan);

    public sealed record class InferType(SourceLocation InferLocation) : Type(new SourceSpan(InferLocation));

    /// <summary>
    /// Examples:
    ///   - int
    ///   - i32
    ///   - void
    ///   - rawptr
    ///   - noreturn
    /// </summary>
    /// <param name="BuiltInKeyword"></param>
    public sealed record class BuiltInType(Token.Keyword BuiltInKeyword) : Type(BuiltInKeyword.SourceSpan)
    {
        public override string ToString() => BuiltInKeyword.Kind.ToTokenString();
    }

    /// <summary>
    /// Examples:
    ///   - string_builder
    ///   - my_struct
    ///   - library::lib_type
    ///   - global::exported_type
    /// </summary>
    /// <param name="TypePath"></param>
    public sealed record class NamedType(PathPart TypePath) : Type(TypePath.SourceSpan);

    /// <summary>
    /// Examples:
    ///   - int *
    ///   - vec3 readonly*
    ///   - string[] *
    /// </summary>
    public sealed record class PointerType(Type ElementType, Modifier[] Modifiers, Token.Operator PointerSymbol)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, PointerSymbol.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples:
    ///   - u8[*]
    ///   - vec4 readonly[*]
    /// </summary>
    public sealed record class BufferType(Type ElementType, Modifier[] Modifiers, Token.Delimiter OpenBracket, Token.Operator PointerSymbol, Token.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[8]
    ///   - vec4 readonly[12]
    /// </summary>
    public sealed record class ArrayType(Type ElementType, Modifier[] Modifiers, Token.Delimiter OpenBracket, Expr[] RankCounts, Token.Delimiter[] RankDelimiters, Token.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[]
    ///   - vec4 readonly[]
    /// </summary>
    public sealed record class SliceType(Type ElementType, Modifier[] Modifiers, Token.Delimiter OpenBracket, Token.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[dynamic]
    ///   - vec4 readonly[dynamic]
    /// </summary>
    public sealed record class DynamicArrayType(Type ElementType, Modifier[] Modifiers, Token.Delimiter OpenBracket, Token.Keyword DynamicKeyword, Token.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[string]
    ///   - vec4 readonly[string, SomeEnum]
    /// </summary>
    public sealed record class ContainerType(Type ElementType, Modifier[] Modifiers, Token.Delimiter OpenBracket, AstNode[] Elements, Token.Delimiter[] ElementDelimiters, Token.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    public sealed record class FunctionType(Type ReturnType, Token.Operator OpenParen, Token.Operator CloseParen)
        : Type(new SourceSpan(ReturnType.SourceSpan.StartLocation, CloseParen.SourceSpan.EndLocation));

    #endregion

    #region Expressions

    public abstract record class Expr(SourceSpan SourceSpan) : AstNode(SourceSpan);

    public sealed record class Integer(Token.Integer Literal) : Expr(Literal.SourceSpan)
    {
        public ulong LiteralValue => Literal.LiteralValue;
    }

    public sealed record class Float(Token.Float Literal) : Expr(Literal.SourceSpan)
    {
        public double LiteralValue => Literal.LiteralValue;
    }

    public sealed record class String(Token.String Literal) : Expr(Literal.SourceSpan)
    {
        public string LiteralValue => Literal.LiteralValue;
    }

    public sealed record class NameLookup(Token.Identifier Name) : Expr(Name.SourceSpan)
    {
        public string Image => Name.Image;
    }

    public sealed record class PathLookup(PathPart Path) : Expr(Path.SourceSpan);

    public sealed record class Block(Token.Delimiter Start, Token.Delimiter End, Stmt[] Body)
        : Expr(new SourceSpan(Start.SourceSpan.StartLocation, End.SourceSpan.EndLocation));

    public sealed record class If(Token.Keyword IfKeyword, Token.Delimiter ConditionStart, Token.Delimiter ConditionEnd, Expr Condition, Expr IfBody, Token.Keyword? ElseKeyword, Expr? ElseBody)
        : Expr(new SourceSpan(IfKeyword.SourceSpan.StartLocation, (ElseBody ?? IfBody).SourceSpan.EndLocation));
    public sealed record class While(Token.Keyword WhileKeyword, Token.Delimiter ConditionStart, Token.Delimiter ConditionEnd, Expr Condition, Expr WhileBody, Token.Keyword? ElseKeyword, Expr? ElseBody)
        : Expr(new SourceSpan(WhileKeyword.SourceSpan.StartLocation, (ElseBody ?? WhileBody).SourceSpan.EndLocation));

    #endregion

    #region Statements

    public abstract record class Stmt(SourceSpan SourceSpan) : AstNode(SourceSpan);

    public sealed record class ExpressionStatement(Expr Expression) : Stmt(Expression.SourceSpan);

    public sealed record class FileNamespaceDeclaration(Token.Keyword NamespaceKeyword, NamePathPart[] NameParts, Token.Delimiter NameDelimiters, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class ScopedNamespaceDeclaration(Token.Keyword NamespaceKeyword, NamePathPart[] NameParts, Token.Delimiter NameDelimiters, Token.Delimiter OpenBrace, Stmt Declarations, Token.Delimiter CloseBrace)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, CloseBrace.SourceSpan.EndLocation));

    public abstract record class FunctionBody(SourceSpan SourceSpan) : IHasSourceSpan;
    public sealed record class EmptyFunctionBody(Token.Delimiter SemiColon) : FunctionBody(SemiColon.SourceSpan);
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody(BodyBlock.SourceSpan);
    public sealed record class ExpressionFunctionBody(Token.Delimiter Arrow, Expr BodyExpression, Token.Delimiter SemiColon)
        : FunctionBody(new SourceSpan(Arrow.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

    public sealed record class FunctionDeclaration(Modifier[] Modifiers, Type ReturnType, Token.Delimiter OpenParams, ParamData[] Parameters, Token.Delimiter[] ParameterSeparators, Token.Delimiter CloseParams, FunctionBody Body)
        : Stmt(new SourceSpan((Modifiers.Length == 0 ? (IHasSourceSpan)ReturnType : Modifiers[0]).SourceSpan.StartLocation, Body.SourceSpan.EndLocation));

    public sealed record class Return(Token.Keyword ReturnKeyword, Expr? ReturnValue, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(ReturnKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Break(Token.Keyword BreakKeyword, Expr? BreakValue, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(BreakKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Continue(Token.Keyword ContinueKeyword, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(ContinueKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Yield(Token.Keyword YieldKeyword, Expr? BreakValue, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class YieldBreak(Token.Keyword YieldKeyword, Token.Keyword BreakKeyword, Token.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

    #endregion
}

public static class AstModifierExt
{
    private static readonly Dictionary<Type, ModifierContext> m_contexts = new();

    public static ModifierContext GetAllowedContexts(this AstNode.Modifier modifier)
    {
        var modifierType = modifier.GetType();
        if (!m_contexts.TryGetValue(modifierType, out var contexts))
        {
            var attr = modifierType.GetCustomAttribute<ModifierAllowedContextsAttribute>();
            if (attr is null)
                throw new InvalidOperationException($"Modifier type {modifier.GetType()} did not have a {nameof(ModifierAllowedContextsAttribute)}");

            m_contexts[modifierType] = contexts = attr.ModifierContext;
        }

        return contexts;
    }
}
