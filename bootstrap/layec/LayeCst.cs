namespace laye;

internal sealed record class LayeCstRoot(LayeCst[] TopLevelNodes)
{
}

internal abstract record class LayeCst(SourceSpan SourceSpan) : IHasSourceSpan
{
    #region Shared Data

    public sealed record class ParamData(SourceSpan SourceSpan, Symbol Symbol, Expr? DefaultValue) : LayeCst(SourceSpan);

    #endregion

    #region Modifiers

    public abstract record class Modifier(SourceSpan SourceSpan) : LayeCst(SourceSpan);

    public sealed record class ExternModifier(LayeToken.Keyword ExternKeyword, LayeToken.String LibraryName) : Modifier(SourceSpan.Combine(ExternKeyword, LibraryName));
    public sealed record class Visibility(LayeToken.Keyword VisibilityKeyword) : Modifier(VisibilityKeyword.SourceSpan);
    public sealed record class CallingConvention(LayeToken.Keyword ConventionKeyword) : Modifier(ConventionKeyword.SourceSpan);
    public sealed record class FunctionHint(LayeToken.Keyword HintKeyword) : Modifier(HintKeyword.SourceSpan);
    public sealed record class Accessibility(LayeToken.Keyword AccessKeyword) : Modifier(AccessKeyword.SourceSpan);

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

    public sealed record class BindingDeclaration(Modifier[] Modifiers, LayeToken.Identifier BindingName, Symbol BindingSymbol, Expr? Expression)
        : Stmt(SourceSpan.Combine(new IHasSourceSpan?[] { BindingName, Expression }));

    public abstract record class FunctionBody;
    public sealed record class EmptyFunctionBody : FunctionBody;
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody;
    public sealed record class ExpressionFunctionBody(Expr BodyExpression) : FunctionBody;

    public sealed record class FunctionDeclaration(FunctionModifiers Modifiers, LayeToken.Identifier FunctionName, Symbol.Function FunctionSymbol, FunctionBody Body)
        : Stmt(FunctionName.SourceSpan);

    #endregion
}

internal sealed record class FunctionModifiers
{
    public LayeCst.ExternModifier? ExternModifier { get; set; }
    public LayeCst.CallingConvention? CallingConvention { get; set; }
}
