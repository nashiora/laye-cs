﻿namespace laye;

internal sealed record class LayeCstRoot(LayeCst[] TopLevelNodes)
{
}

internal abstract record class LayeCst(SourceSpan SourceSpan) : IHasSourceSpan
{
    #region Shared Data

    public sealed record class ParamData(SourceSpan SourceSpan, Symbol Symbol, Expr? DefaultValue) : LayeCst(SourceSpan);

    #endregion

    #region Modifiers

    public sealed record class FunctionModifiers
    {
        public string? ExternLibrary { get; set; }
        public VisibilityKind Visibility { get; set; } = VisibilityKind.Internal;
        public CallingConvention CallingConvention { get; set; } = CallingConvention.Laye;
        public FunctionHintKind FunctionHint { get; set; } = FunctionHintKind.None;
    }

    #endregion

    #region Expressions

    public abstract record class Expr(SourceSpan SourceSpan, SymbolType Type) : LayeCst(SourceSpan);

    public sealed record class Integer(LayeToken.Integer Literal, SymbolType Type) : Expr(Literal.SourceSpan, Type);
    public sealed record class Float(LayeToken.Float Literal, SymbolType Type) : Expr(Literal.SourceSpan, Type);
    public sealed record class Bool(LayeToken.Keyword Literal, SymbolType Type) : Expr(Literal.SourceSpan, Type);
    public sealed record class String(LayeToken.String Literal, SymbolType Type) : Expr(Literal.SourceSpan, Type);

    public sealed record class LoadValue(SourceSpan SourceSpan, Symbol Symbol) : Expr(SourceSpan, Symbol.Type!);

    public sealed record class TypeCast(SourceSpan SourceSpan, Expr Expression, SymbolType TargetType) : Expr(SourceSpan, TargetType);

    public sealed record class InvokeFunction(SourceSpan SourceSpan, Symbol.Function TargetFunctionSymbol, Expr[] Arguments) : Expr(SourceSpan, TargetFunctionSymbol.Type!.ReturnType);

    #endregion

    #region Statements

    public abstract record class Stmt(SourceSpan SourceSpan) : LayeCst(SourceSpan);

    public sealed record class ExpressionStatement(Expr Expression) : Stmt(Expression.SourceSpan);

    public sealed record class Block(SourceSpan SourceSpan, Stmt[] Body) : Stmt(SourceSpan);

    public sealed record class BindingDeclaration(LayeToken.Identifier BindingName, Symbol BindingSymbol, Expr? Expression)
        : Stmt(SourceSpan.Combine(new IHasSourceSpan?[] { BindingName, Expression }));

    public abstract record class FunctionBody;
    public sealed record class EmptyFunctionBody : FunctionBody;
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody;
    public sealed record class ExpressionFunctionBody(Expr BodyExpression) : FunctionBody;

    public sealed record class FunctionDeclaration(FunctionModifiers Modifiers, LayeToken.Identifier FunctionName, Symbol.Function FunctionSymbol, FunctionBody Body)
        : Stmt(FunctionName.SourceSpan);

    #endregion
}
