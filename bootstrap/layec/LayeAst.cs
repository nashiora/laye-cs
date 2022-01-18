using System.Reflection;

namespace laye;

public abstract record class LayeAst(SourceSpan SourceSpan) : IHasSourceSpan
{
    #region Shared Data

    public sealed record class BindingData(Modifier[] Modifiers, Type BindingType, LayeToken.Identifier BindingName)
        : LayeAst(new SourceSpan((Modifiers.Length == 0 ? (IHasSourceSpan)BindingType : Modifiers[0]).SourceSpan.StartLocation, BindingName.SourceSpan.EndLocation));

    public sealed record class ParamData(BindingData Binding, LayeToken.Operator? AssignOperator, Expr? DefaultValue)
        : LayeAst(new SourceSpan(Binding.SourceSpan.StartLocation, (DefaultValue ?? (IHasSourceSpan)Binding).SourceSpan.EndLocation));

    public abstract record class PathPart(SourceSpan SourceSpan);
    public sealed record class EmptyPathPart(SourceLocation Location) : PathPart(new SourceSpan(Location, Location));
    public sealed record class GlobalPathPart(LayeToken.Keyword GlobalKeyword) : PathPart(GlobalKeyword.SourceSpan);
    public sealed record class NamePathPart(LayeToken.Identifier Name) : PathPart(Name.SourceSpan);
    public sealed record class JoinedPath(PathPart BasePath, LayeToken.Delimiter PathSeparator, NamePathPart LookupPart)
        : PathPart(new SourceSpan(BasePath.SourceSpan.StartLocation, LookupPart.SourceSpan.EndLocation));

    public abstract record class TypeParam(LayeToken.Identifier Name) : LayeAst(Name.SourceSpan);
    public sealed record class TypeParamType(LayeToken.Identifier Name) : TypeParam(Name);
    public sealed record class TypeParamConstant(Type ConstantType, LayeToken.Identifier Name) : TypeParam(Name);

    public sealed record class TypeParamList(LayeToken.Delimiter OpenAngle, TypeParam[] TypeParams, LayeToken.Delimiter CloseAngle);

    #endregion

    #region Modifiers

    public abstract record class Modifier(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class Visibility(LayeToken.Keyword VisibilityKeyword) : Modifier(VisibilityKeyword.SourceSpan);
    public sealed record class CallingConvention(LayeToken.Keyword ConventionKeyword) : Modifier(ConventionKeyword.SourceSpan);
    public sealed record class FunctionHint(LayeToken.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);
    public sealed record class Accessibility(LayeToken.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);

    #endregion

    #region Types

    public abstract record class Type(SourceSpan SourceSpan) : LayeAst(SourceSpan);

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
    public sealed record class BuiltInType(LayeToken.Keyword BuiltInKeyword) : Type(BuiltInKeyword.SourceSpan)
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
    public sealed record class PointerType(Type ElementType, Modifier[] Modifiers, LayeToken.Operator PointerSymbol)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, PointerSymbol.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples:
    ///   - u8[*]
    ///   - vec4 readonly[*]
    /// </summary>
    public sealed record class BufferType(Type ElementType, Modifier[] Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Operator PointerSymbol, LayeToken.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[8]
    ///   - vec4 readonly[12]
    /// </summary>
    public sealed record class ArrayType(Type ElementType, Modifier[] Modifiers, LayeToken.Delimiter OpenBracket, Expr[] RankCounts, LayeToken.Delimiter[] RankDelimiters, LayeToken.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[]
    ///   - vec4 readonly[]
    /// </summary>
    public sealed record class SliceType(Type ElementType, Modifier[] Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[dynamic]
    ///   - vec4 readonly[dynamic]
    /// </summary>
    public sealed record class DynamicArrayType(Type ElementType, Modifier[] Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Keyword DynamicKeyword, LayeToken.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    /// <summary>
    /// Examples: 
    ///   - u8[string]
    ///   - vec4 readonly[string, SomeEnum]
    /// </summary>
    public sealed record class ContainerType(Type ElementType, Modifier[] Modifiers, LayeToken.Delimiter OpenBracket, LayeAst[] Elements, LayeToken.Delimiter[] ElementDelimiters, LayeToken.Delimiter CloseBracket)
        : Type(new SourceSpan(ElementType.SourceSpan.StartLocation, CloseBracket.SourceSpan.EndLocation))
    {
        public bool IsReadOnly => Modifiers.Any(m => m is Accessibility acc && acc.AccessKeyword.Kind == Keyword.ReadOnly);
    }

    public sealed record class FunctionType(Type ReturnType, LayeToken.Operator OpenParen, LayeToken.Operator CloseParen)
        : Type(new SourceSpan(ReturnType.SourceSpan.StartLocation, CloseParen.SourceSpan.EndLocation));

    #endregion

    #region Expressions

    public abstract record class Expr(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class Integer(LayeToken.Integer Literal) : Expr(Literal.SourceSpan)
    {
        public ulong LiteralValue => Literal.LiteralValue;
    }

    public sealed record class Float(LayeToken.Float Literal) : Expr(Literal.SourceSpan)
    {
        public double LiteralValue => Literal.LiteralValue;
    }

    public sealed record class String(LayeToken.String Literal) : Expr(Literal.SourceSpan)
    {
        public string LiteralValue => Literal.LiteralValue;
    }

    public sealed record class NameLookup(LayeToken.Identifier Name) : Expr(Name.SourceSpan)
    {
        public string Image => Name.Image;
    }

    public sealed record class PathLookup(PathPart Path) : Expr(Path.SourceSpan);

    public sealed record class Block(LayeToken.Delimiter Start, LayeToken.Delimiter End, Stmt[] Body)
        : Expr(new SourceSpan(Start.SourceSpan.StartLocation, End.SourceSpan.EndLocation));

    public sealed record class Invoke(Expr TargetExpression, LayeToken.Delimiter OpenArgs, Expr[] Arguments, LayeToken.Delimiter[] ArgumentDelimiters, LayeToken.Delimiter CloseArgs)
        : Expr(new SourceSpan(TargetExpression.SourceSpan.StartLocation, CloseArgs.SourceSpan.EndLocation));

    public sealed record class If(LayeToken.Keyword IfKeyword, LayeToken.Delimiter ConditionStart, LayeToken.Delimiter ConditionEnd, Expr Condition, Expr IfBody, LayeToken.Keyword? ElseKeyword, Expr? ElseBody)
        : Expr(new SourceSpan(IfKeyword.SourceSpan.StartLocation, (ElseBody ?? IfBody).SourceSpan.EndLocation));
    public sealed record class While(LayeToken.Keyword WhileKeyword, LayeToken.Delimiter ConditionStart, LayeToken.Delimiter ConditionEnd, Expr Condition, Expr WhileBody, LayeToken.Keyword? ElseKeyword, Expr? ElseBody)
        : Expr(new SourceSpan(WhileKeyword.SourceSpan.StartLocation, (ElseBody ?? WhileBody).SourceSpan.EndLocation));

    #endregion

    #region Statements

    public abstract record class Stmt(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class ExpressionStatement(Expr Expression) : Stmt(Expression.SourceSpan);

    public sealed record class FileNamespaceDeclaration(LayeToken.Keyword NamespaceKeyword, NamePathPart[] NameParts, LayeToken.Delimiter NameDelimiters, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class ScopedNamespaceDeclaration(LayeToken.Keyword NamespaceKeyword, NamePathPart[] NameParts, LayeToken.Delimiter NameDelimiters, LayeToken.Delimiter OpenBrace, Stmt Declarations, LayeToken.Delimiter CloseBrace)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, CloseBrace.SourceSpan.EndLocation));

    public abstract record class FunctionBody(SourceSpan SourceSpan) : IHasSourceSpan;
    public sealed record class EmptyFunctionBody(LayeToken.Delimiter SemiColon) : FunctionBody(SemiColon.SourceSpan);
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody(BodyBlock.SourceSpan);
    public sealed record class ExpressionFunctionBody(LayeToken.Delimiter Arrow, Expr BodyExpression, LayeToken.Delimiter SemiColon)
        : FunctionBody(new SourceSpan(Arrow.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

    public sealed record class FunctionDeclaration(Modifier[] Modifiers, Type ReturnType, LayeToken.Identifier Name, LayeToken.Delimiter OpenParams, ParamData[] Parameters, LayeToken.Delimiter[] ParameterSeparators, LayeToken.Delimiter CloseParams, FunctionBody Body)
        : Stmt(new SourceSpan((Modifiers.Length == 0 ? (IHasSourceSpan)ReturnType : Modifiers[0]).SourceSpan.StartLocation, Body.SourceSpan.EndLocation));

    public sealed record class Return(LayeToken.Keyword ReturnKeyword, Expr? ReturnValue, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(ReturnKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Break(LayeToken.Keyword BreakKeyword, Expr? BreakValue, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(BreakKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Continue(LayeToken.Keyword ContinueKeyword, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(ContinueKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class Yield(LayeToken.Keyword YieldKeyword, Expr? BreakValue, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class YieldBreak(LayeToken.Keyword YieldKeyword, LayeToken.Keyword BreakKeyword, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

    #endregion
}
