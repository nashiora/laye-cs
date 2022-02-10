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

    public sealed record class FunctionModifiers
    {
        public string? ExternLibrary { get; set; }
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
    public sealed record class NamedIndex(Expr TargetExpression, LayeToken.Identifier Name, SymbolType FieldType)
        : Expr(SourceSpan.Combine(TargetExpression, Name), FieldType);
    public sealed record class StringLengthLookup(SourceSpan SourceSpan, Expr TargetExpression)
        : Expr(SourceSpan, new SymbolType.Integer(false));
    public sealed record class StringDataLookup(SourceSpan SourceSpan, Expr TargetExpression)
        : Expr(SourceSpan, new SymbolType.Buffer(new SymbolType.SizedInteger(false, 8), AccessKind.ReadOnly));
    public sealed record class DynamicIndex(SourceSpan SourceSpan, Expr TargetExpression, Expr[] Arguments, SymbolType Type)
        : Expr(SourceSpan, Type);
    public sealed record class Slice(SourceSpan SourceSpan, Expr TargetExpression, Expr? OffsetExpression, Expr? CountExpression, SymbolType ElementType)
        : Expr(SourceSpan, new SymbolType.Slice(ElementType));
    public sealed record class Substring(SourceSpan SourceSpan, Expr TargetExpression, Expr? OffsetExpression, Expr? CountExpression)
        : Expr(SourceSpan, new SymbolType.String());

    public sealed record class TypeCast(SourceSpan SourceSpan, Expr Expression, SymbolType TargetType) : Expr(SourceSpan, TargetType);
    public sealed record class SliceToString(Expr SliceExpression) : Expr(SliceExpression.SourceSpan, new SymbolType.String());

    public sealed record class InvokeFunction(SourceSpan SourceSpan, Symbol.Function TargetFunctionSymbol, Expr[] Arguments) : Expr(SourceSpan, TargetFunctionSymbol.Type!.ReturnType);

    #endregion

    #region Statements

    public abstract record class Stmt(SourceSpan SourceSpan) : LayeCst(SourceSpan);

    public sealed record class ExpressionStatement(Expr Expression) : Stmt(Expression.SourceSpan);
    public sealed record class Assignment(Expr TargetExpression, Expr ValueExpression) : Stmt(SourceSpan.Combine(TargetExpression, ValueExpression));

    public sealed record class Block(SourceSpan SourceSpan, Stmt[] Body) : Stmt(SourceSpan);
    public sealed record class DeadCode(SourceSpan SourceSpan, Stmt[] Body) : Stmt(SourceSpan);

    public sealed record class BindingDeclaration(LayeToken.Identifier BindingName, Symbol BindingSymbol, Expr? Expression)
        : Stmt(SourceSpan.Combine(new IHasSourceSpan?[] { BindingName, Expression }));

    public abstract record class FunctionBody;
    public sealed record class EmptyFunctionBody : FunctionBody;
    public sealed record class BlockFunctionBody(Block BodyBlock) : FunctionBody;
    public sealed record class ExpressionFunctionBody(Expr BodyExpression) : FunctionBody;

    public sealed record class FunctionDeclaration(FunctionModifiers Modifiers, LayeToken.Identifier FunctionName, Symbol.Function FunctionSymbol, Symbol.Binding[] ParameterSymbols, FunctionBody Body)
        : Stmt(FunctionName.SourceSpan);

    public sealed record class Return(SourceSpan SourceSpan, Expr? ReturnValue)
        : Stmt(SourceSpan);

    #endregion
}

internal static class LayeCstExtensions
{
    public static bool CheckReturns(this LayeCst.Stmt node) => node switch
    {
        LayeCst.Return => true,
        LayeCst.Block block => block.Body.Any(child => child.CheckReturns()),
        _ => false,
    };

    public static bool CheckIsLValue(this LayeCst.Expr node) => node switch
    {
        LayeCst.LoadValue => true,
        LayeCst.NamedIndex => true,
        LayeCst.DynamicIndex dyn => dyn.TargetExpression.CheckIsLValue(),
        // TODO(local): a typecast is also valid, but we aren't worried about that for now.
        _ => false,
    };
}
