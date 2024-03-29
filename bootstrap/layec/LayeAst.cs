﻿namespace laye;

internal sealed record class LayeAstRoot(string FileName, LayeAst[] TopLevelNodes)
{
}

internal abstract record class LayeAst(SourceSpan SourceSpan) : IHasSourceSpan
{
    #region Shared Data

    public sealed record class BindingData(Type BindingType, LayeToken.Identifier BindingName)
        : LayeAst(SourceSpan.Combine(BindingType, BindingName));

    public sealed record class ParamData(BindingData Binding, LayeToken.Operator? AssignOperator, Expr? DefaultValue)
        : LayeAst(SourceSpan.Combine(new IHasSourceSpan?[] { Binding, DefaultValue }));

    // NOTE(local): this implies that union variants cannot have 0 fields when a field list is given as that would appear identical to a non-union variant
    public sealed record class VariantData(LayeToken.Identifier VariantName, Expr? VariantValue, ParamData[] VariantFields)
        : LayeAst(SourceSpan.Combine(VariantName, VariantValue, VariantFields.LastOrDefault()));

    public abstract record class PathPart(SourceSpan SourceSpan) : IHasSourceSpan;
    public sealed record class EmptyPathPart(SourceLocation Location) : PathPart(new SourceSpan(Location));
    public sealed record class GlobalPathPart(LayeToken.Keyword GlobalKeyword) : PathPart(GlobalKeyword.SourceSpan);
    public sealed record class NamePathPart(LayeToken Name) : PathPart(Name.SourceSpan);
    public sealed record class JoinedPath(PathPart BasePath, NamePathPart LookupPart) : PathPart(SourceSpan.Combine(BasePath, LookupPart));

    public abstract record class TypeParam(LayeToken.Identifier Name) : LayeAst(Name.SourceSpan);
    public sealed record class TypeParamType(LayeToken.Identifier Name) : TypeParam(Name);
    public sealed record class TypeParamConstant(Type ConstantType, LayeToken.Identifier Name) : TypeParam(Name);

    public sealed record class TypeParamList(LayeToken.Delimiter OpenAngle, TypeParam[] TypeParams, LayeToken.Delimiter CloseAngle);

    #endregion

    #region Modifiers

    public abstract record class Modifier(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class ExternModifier(LayeToken.Keyword ExternKeyword, LayeToken.String LibraryName) : Modifier(SourceSpan.Combine(ExternKeyword, LibraryName));
    public sealed record class CallingConventionModifier(LayeToken.Keyword ConventionKeyword) : Modifier(ConventionKeyword.SourceSpan);
    public sealed record class FunctionHintModifier(LayeToken.Keyword HintKeyword) : Modifier(HintKeyword.SourceSpan);
    public sealed record class AccessModifier(LayeToken.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);

    public sealed record class ContainerModifiers : IHasSourceSpan
    {
        public SourceSpan SourceSpan => AccessModifier?.SourceSpan ?? SourceSpan.Invalid;
        public bool IsEmpty => AccessModifier is null;

        public AccessModifier? AccessModifier { get; set; }
        public AccessKind Access => AccessModifier?.AccessKeyword.Kind switch
        {
            Keyword.ReadOnly => AccessKind.ReadOnly,
            Keyword.WriteOnly => AccessKind.WriteOnly,
            Keyword.Const => AccessKind.Constant,
            _ => AccessKind.ReadWrite,
        };
    }

    public sealed record class FunctionModifiers : IHasSourceSpan
    {
        public SourceSpan SourceSpan => IsEmpty ? SourceSpan.Invalid : SourceSpan.Combine(ExternModifier, CallingConventionModifier, FunctionHintModifier);
        public bool IsEmpty => ExternModifier is null && CallingConventionModifier is null && FunctionHintModifier is null;

        public ExternModifier? ExternModifier { get; set; }
        public string? ExternLibrary => ExternModifier?.LibraryName.LiteralValue;

        public CallingConventionModifier? CallingConventionModifier { get; set; }
        public CallingConvention CallingConvention => CallingConventionModifier?.ConventionKeyword.Kind switch
        {
            Keyword.NoContext => CallingConvention.LayeNoContext,
            Keyword.CDecl => CallingConvention.CDecl,
            Keyword.FastCall => CallingConvention.FastCall,
            Keyword.StdCall => CallingConvention.StdCall,
            _ => CallingConvention.Laye,
        };

        public FunctionHintModifier? FunctionHintModifier { get; set; }
        public FunctionHintKind FunctionHint => FunctionHintModifier?.HintKeyword.Kind switch
        {
            Keyword.Intrinsic => FunctionHintKind.Intrinsic,
            Keyword.Export => FunctionHintKind.Export,
            Keyword.Inline => FunctionHintKind.Inline,
            _ => FunctionHintKind.None,
        };
    }

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
    public sealed record class PointerType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Operator PointerSymbol)
        : Type(SourceSpan.Combine(ElementType, PointerSymbol));

    /// <summary>
    /// Examples:
    ///   - u8[*]
    ///   - vec4 readonly[*]
    /// </summary>
    public sealed record class BufferType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Operator PointerSymbol, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));

    /// <summary>
    /// Examples: 
    ///   - u8[8]
    ///   - vec4 readonly[12]
    /// </summary>
    public sealed record class ArrayType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, Expr[] RankCounts, LayeToken.Delimiter[] RankDelimiters, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));

    /// <summary>
    /// Examples: 
    ///   - u8[]
    ///   - vec4 readonly[]
    /// </summary>
    public sealed record class SliceType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));

    /// <summary>
    /// Examples: 
    ///   - u8[]
    ///   - vec4 readonly[]
    /// </summary>
    public sealed record class DynamicType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Keyword DynamicKeyword, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));

#if false // less useful types for now
    /// <summary>
    /// Examples: 
    ///   - u8[dynamic]
    ///   - vec4 readonly[dynamic]
    /// </summary>
    public sealed record class DynamicArrayType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, LayeToken.Keyword DynamicKeyword, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));

    /// <summary>
    /// Examples: 
    ///   - u8[string]
    ///   - vec4 readonly[string, SomeEnum]
    /// </summary>
    public sealed record class ContainerType(Type ElementType, ContainerModifiers Modifiers, LayeToken.Delimiter OpenBracket, LayeAst[] Elements, LayeToken.Delimiter[] ElementDelimiters, LayeToken.Delimiter CloseBracket)
        : Type(SourceSpan.Combine(ElementType, CloseBracket));
#endif

    public sealed record class FunctionType(FunctionModifiers Modifiers, Type ReturnType, Type[] ParameterTypes, VarArgsKind VarArgsKind)
        : Type(SourceSpan.Combine(Modifiers, ReturnType));

#endregion

#region Expressions

    public abstract record class Expr(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class Integer(LayeToken.Integer Literal, bool Signed = false) : Expr(Literal.SourceSpan);
    public sealed record class Float(LayeToken.Float Literal) : Expr(Literal.SourceSpan);
    public sealed record class Bool(LayeToken.Keyword Literal) : Expr(Literal.SourceSpan);
    public sealed record class String(LayeToken.String Literal) : Expr(Literal.SourceSpan);
    public sealed record class NullPtr(LayeToken.Keyword Literal) : Expr(Literal.SourceSpan);
    public sealed record class Nil(LayeToken.Keyword Literal) : Expr(Literal.SourceSpan);

    public sealed record class NameLookup(LayeToken.Identifier Name) : Expr(Name.SourceSpan);
    public sealed record class PathLookup(PathPart Path) : Expr(Path.SourceSpan);
    public sealed record class NamedIndex(Expr TargetExpression, LayeToken.Identifier Name) : Expr(SourceSpan.Combine(TargetExpression, Name));
    public sealed record class DynamicIndex(Expr TargetExpression, Expr[] Arguments) : Expr(SourceSpan.Combine(TargetExpression, Arguments.LastOrDefault()));
    public sealed record class Slice(Expr TargetExpression, Expr? OffsetExpression, Expr? CountExpression)
        : Expr(SourceSpan.Combine(TargetExpression, OffsetExpression, CountExpression));

    public sealed record class GroupedExpression(LayeToken.Delimiter OpenGroup, Expr Expression, LayeToken.Delimiter CloseGroup)
        : Expr(new SourceSpan(OpenGroup.SourceSpan.StartLocation, CloseGroup.SourceSpan.EndLocation));

    public sealed record class Cast(LayeToken.Keyword CastKeyword, Type TargetType, Expr TargetExpression)
        : Expr(SourceSpan.Combine(CastKeyword, TargetExpression));
    public sealed record class SizeOfType(Type _Type) : Expr(_Type.SourceSpan);
    public sealed record class SizeOfExpression(Expr Expression) : Expr(Expression.SourceSpan);
    public sealed record class NameOfVariant(Expr Expression) : Expr(Expression.SourceSpan);

    public sealed record class PrefixOperation(LayeToken.Operator Operator, Expr Expression)
        : Expr(SourceSpan.Combine(Operator, Expression));
    public sealed record class LogicalNot(Expr Expression)
        : Expr(Expression.SourceSpan);

    public sealed record class InfixOperation(Expr LeftExpression, LayeToken.Operator Operator, Expr RightExpression)
        : Expr(SourceSpan.Combine(LeftExpression, RightExpression));
    public sealed record class LogicalInfixOperation(Expr LeftExpression, LayeToken.Keyword Keyword, Expr RightExpression)
        : Expr(SourceSpan.Combine(LeftExpression, RightExpression));

    public sealed record class Invoke(Expr TargetExpression, LayeToken.Delimiter OpenArgs, Expr[] Arguments, LayeToken.Delimiter[] ArgumentDelimiters, LayeToken.Delimiter CloseArgs)
        : Expr(new SourceSpan(TargetExpression.SourceSpan.StartLocation, CloseArgs.SourceSpan.EndLocation));

#endregion

#region Statements

    public abstract record class Stmt(SourceSpan SourceSpan) : LayeAst(SourceSpan);

    public sealed record class ExpressionStatement(Expr Expression, LayeToken.Delimiter Terminator) : Stmt(Expression.SourceSpan);
    public sealed record class Assignment(Expr TargetExpression, Expr ValueExpression) : Stmt(SourceSpan.Combine(TargetExpression, ValueExpression));
    public sealed record class DynamicAppend(SourceSpan SourceSpan, Expr TargetExpression, Expr ValueExpression) : Stmt(SourceSpan);
    public sealed record class DynamicFree(SourceSpan SourceSpan, Expr TargetExpression) : Stmt(SourceSpan);

    public sealed record class Block(LayeToken.Delimiter Start, LayeToken.Delimiter End, Stmt[] Body)
        : Stmt(new SourceSpan(Start.SourceSpan.StartLocation, End.SourceSpan.EndLocation));

    public sealed record class If(Expr Condition, Stmt IfBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Condition, IfBody, ElseBody));
    public sealed record class IfIs(Expr ValueExpression, bool IsNot, LayeToken.Identifier VariantName, LayeToken.Identifier? BoundName, Stmt IfBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(ValueExpression, IfBody, ElseBody));
    public sealed record class IfIsNil(Expr ValueExpression, bool IsNot, Stmt IfBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(ValueExpression, IfBody, ElseBody));
    public sealed record class While(Expr Condition, Stmt WhileBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Condition, WhileBody, ElseBody));
    public sealed record class CFor(Stmt? Initializer, Expr? Condition, Stmt? Iterator, Stmt ForBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Initializer, Condition, Iterator, ForBody, ElseBody));

    public sealed record class FileNamespaceDeclaration(LayeToken.Keyword NamespaceKeyword, NamePathPart[] NameParts, LayeToken.Delimiter NameDelimiters, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class ScopedNamespaceDeclaration(LayeToken.Keyword NamespaceKeyword, NamePathPart[] NameParts, LayeToken.Delimiter NameDelimiters, LayeToken.Delimiter OpenBrace, Stmt Declarations, LayeToken.Delimiter CloseBrace)
        : Stmt(new SourceSpan(NamespaceKeyword.SourceSpan.StartLocation, CloseBrace.SourceSpan.EndLocation));

    public sealed record class BindingDeclaration(Type BindingType, LayeToken.Identifier Name, LayeToken.Operator? Assign, Expr? Expression, LayeToken.Delimiter SemiColon)
        : Stmt(SourceSpan.Combine(BindingType, SemiColon));

    public abstract record class FunctionBody(SourceSpan SourceSpan) : IHasSourceSpan;
    public sealed record class EmptyFunctionBody(LayeToken.Delimiter SemiColon) : FunctionBody(SemiColon.SourceSpan);
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody(BodyBlock.SourceSpan);
    public sealed record class ExpressionFunctionBody(LayeToken.Delimiter Arrow, Expr BodyExpression, LayeToken.Delimiter SemiColon)
        : FunctionBody(new SourceSpan(Arrow.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

    public sealed record class StructDeclaration(LayeToken.Keyword StructKeyword, LayeToken.Identifier Name, ParamData[] Fields)
        : Stmt(SourceSpan.Combine(StructKeyword, Name));
    public sealed record class EnumDeclaration(LayeToken.Keyword EnumKeyword, LayeToken.Identifier Name, VariantData[] Variants)
        : Stmt(SourceSpan.Combine(EnumKeyword, Name))
    {
        public bool HasBothEnumAndUnionVariants => IsUnion && IsEnum;

        public bool IsEnum => Variants.Any(v => v.VariantValue is not null);
        public bool IsUnion => Variants.Any(v => v.VariantFields.Length > 0);
    }

    public sealed record class FunctionDeclaration(FunctionModifiers Modifiers, Type ReturnType, LayeToken.Identifier Name, LayeToken.Delimiter OpenParams,
        ParamData[] Parameters, LayeToken.Delimiter[] ParameterSeparators, LayeToken.Keyword? VarargsKeyword, VarArgsKind VarArgsKind, LayeToken.Delimiter CloseParams, FunctionBody Body)
        : Stmt(SourceSpan.Combine(Modifiers, ReturnType, Body));

    public sealed record class Return(LayeToken.Keyword ReturnKeyword, Expr? ReturnValue)
        : Stmt(SourceSpan.Combine(ReturnKeyword, ReturnValue));
    public sealed record class Break(LayeToken.Keyword BreakKeyword, Expr? BreakValue) : Stmt(SourceSpan.Combine(BreakKeyword, BreakValue));
    public sealed record class Continue(LayeToken.Keyword ContinueKeyword, Expr? ContinueValue) : Stmt(SourceSpan.Combine(ContinueKeyword, ContinueValue));
    public sealed record class Yield(LayeToken.Keyword YieldKeyword, Expr? BreakValue, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));
    public sealed record class YieldBreak(LayeToken.Keyword YieldKeyword, LayeToken.Keyword BreakKeyword, LayeToken.Delimiter SemiColon)
        : Stmt(new SourceSpan(YieldKeyword.SourceSpan.StartLocation, SemiColon.SourceSpan.EndLocation));

#endregion
}
