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
    public sealed record class NullPtr(LayeToken.Keyword Literal, SymbolType Type, SymbolType ElementType) : Expr(Literal.SourceSpan, Type);

    public sealed record class LoadValue(SourceSpan SourceSpan, Symbol Symbol) : Expr(SourceSpan, Symbol.Type!);
    public sealed record class NamedIndex(Expr TargetExpression, LayeToken.Identifier Name, SymbolType FieldType)
        : Expr(SourceSpan.Combine(TargetExpression, Name), FieldType);
    public sealed record class StringLengthLookup(SourceSpan SourceSpan, Expr TargetExpression)
        : Expr(SourceSpan, new SymbolType.Integer(false));
    public sealed record class StringDataLookup(SourceSpan SourceSpan, Expr TargetExpression)
        : Expr(SourceSpan, new SymbolType.Buffer(new SymbolType.SizedInteger(false, 8), AccessKind.ReadOnly));
    public sealed record class SliceLengthLookup(SourceSpan SourceSpan, Expr TargetExpression)
        : Expr(SourceSpan, new SymbolType.Integer(false));

    public sealed record class DynamicIndex(SourceSpan SourceSpan, Expr TargetExpression, Expr[] Arguments, SymbolType Type)
        : Expr(SourceSpan, Type);
    public sealed record class Slice(SourceSpan SourceSpan, Expr TargetExpression, Expr? OffsetExpression, Expr? CountExpression, SymbolType ElementType)
        : Expr(SourceSpan, new SymbolType.Slice(ElementType));
    public sealed record class Substring(SourceSpan SourceSpan, Expr TargetExpression, Expr? OffsetExpression, Expr? CountExpression)
        : Expr(SourceSpan, new SymbolType.String());

    public sealed record class Negate(Expr Expression) : Expr(Expression.SourceSpan, Expression.Type);
    public sealed record class AddressOf(Expr Expression, AccessKind Access = AccessKind.ReadWrite) : Expr(Expression.SourceSpan, new SymbolType.Pointer(Expression.Type, Access));
    public sealed record class ValueAt(Expr Expression, SymbolType ElementType) : Expr(Expression.SourceSpan, ElementType);
    public sealed record class LogicalNot(Expr Expression) : Expr(Expression.SourceSpan, SymbolTypes.Bool);

    public sealed record class Add(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class Subtract(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class Multiply(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class Divide(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class Remainder(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    
    public sealed record class CompareEqual(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    public sealed record class CompareNotEqual(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    public sealed record class CompareLess(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    public sealed record class CompareLessEqual(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    public sealed record class CompareGreater(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    public sealed record class CompareGreaterEqual(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), SymbolTypes.Bool);
    
    public sealed record class LeftShift(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class RightShift(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class BitwiseAnd(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class BitwiseOr(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class BitwiseXor(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class BitwiseComplement(Expr Expression) : Expr(Expression.SourceSpan, Expression.Type);
    
    public sealed record class LogicalAnd(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);
    public sealed record class LogicalOr(Expr LeftExpression, Expr RightExpression) : Expr(SourceSpan.Combine(LeftExpression, RightExpression), LeftExpression.Type);

    public sealed record class Cast(SourceSpan SourceSpan, SymbolType TargetType, Expr TargetExpression) : Expr(SourceSpan, TargetType);
    public sealed record class SizeOf(SourceSpan SourceSpan, SymbolType TargetType) : Expr(SourceSpan, SymbolTypes.UInt);
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
    
    public sealed record class If(Expr Condition, Stmt IfBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Condition, IfBody, ElseBody));
    public sealed record class While(Expr Condition, Stmt WhileBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Condition, WhileBody, ElseBody));
    public sealed record class CFor(Stmt? Initializer, Expr? Condition, Stmt? Iterator, Stmt ForBody, Stmt? ElseBody) : Stmt(SourceSpan.Combine(Initializer, Condition, Iterator, ForBody, ElseBody));


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
        LayeCst.If _if => _if.IfBody.CheckReturns() && (_if.ElseBody?.CheckReturns() ?? false),
        LayeCst.While _while => _while.WhileBody.CheckReturns() && (_while.ElseBody?.CheckReturns() ?? true),
        _ => false,
    };

    public static bool CheckIsLValue(this LayeCst.Expr node) => node switch
    {
        LayeCst.LoadValue => true,
        LayeCst.NamedIndex => true,
        LayeCst.ValueAt => true,
        LayeCst.DynamicIndex dyn => dyn.TargetExpression.CheckIsLValue(),
        // TODO(local): a typecast is also valid, but we aren't worried about that for now.
        _ => false,
    };
}
