namespace laye;

internal sealed record class LayeIrModule(LayeIr.Function[] Functions, Symbol[] TypeSymbols);

internal abstract record class LayeIr(SourceSpan SourceSpan) : IHasSourceSpan
{
    public sealed record class Identifier(string Image, SourceSpan SourceSpan) : LayeIr(SourceSpan)
    {
        public static implicit operator Identifier(LayeToken.Identifier identifier) => new(identifier.Image, identifier.SourceSpan);
    }

    #region Declarations

    public abstract record class Decl(SourceSpan SourceSpan) : LayeIr(SourceSpan);

    public sealed record class Function(Identifier Name, Symbol.Function Symbol, BasicBlock[] BasicBlocks) : Decl(Name.SourceSpan);

    #endregion

    #region Instructions

    public sealed record class BasicBlock(Insn[] Instructions, BranchInsn BranchInstruction);

    public abstract record class Insn(SourceSpan SourceSpan) : LayeIr(SourceSpan);
    public abstract record class BranchInsn(SourceSpan SourceSpan) : LayeIr(SourceSpan);
    public abstract record class Value(SourceSpan SourceSpan, SymbolType Type) : Insn(SourceSpan);

    public sealed record class ReturnVoid(SourceSpan SourceSpan) : BranchInsn(SourceSpan);
    public sealed record class Return(SourceSpan SourceSpan, Value ReturnValue) : BranchInsn(SourceSpan);

    public sealed record class Integer(SourceSpan SourceSpan, ulong LiteralValue, SymbolType Type) : Value(SourceSpan, Type);
    public sealed record class Float(SourceSpan SourceSpan, double LiteralValue, SymbolType Type) : Value(SourceSpan, Type);
    public sealed record class String(SourceSpan SourceSpan, string LiteralValue, SymbolType Type) : Value(SourceSpan, Type);

    public sealed record class InvokeGlobalFunction(SourceSpan SourceSpan, Symbol.Function GlobalFunction, Value[] Arguments) : Value(SourceSpan, GlobalFunction.Type!.ReturnType);

    public sealed record class IntToRawPtrCast(SourceSpan SourceSpan, Value CastValue) : Value(SourceSpan, new SymbolType.RawPtr());

    #endregion
}

internal sealed class LayeIrFunctionBuilder
{
    private readonly LayeIr.Identifier m_name;
    private readonly Symbol.Function m_symbol;

    private readonly List<LayeIrBasicBlockBuilder> m_basicBlocks = new();

    private int m_index = 0;
    public LayeIrBasicBlockBuilder CurrentBlock { get; private set; } = default!;

    public IEnumerable<LayeIrBasicBlockBuilder> BasicBlocks => m_basicBlocks;

    public SymbolTable FunctionScope { get; }

    private readonly Stack<SymbolTable> m_lexicalScopes = new();
    public SymbolTable CurrentScope => m_lexicalScopes.TryPeek(out var result) ? result : FunctionScope;

    public LayeIrFunctionBuilder(LayeIr.Identifier name, Symbol.Function symbol, SymbolTable functionScope)
    {
        m_name = name;
        m_symbol = symbol;
        FunctionScope = functionScope;
    }

    public void PushScope() => m_lexicalScopes.Push(new(CurrentScope));
    public void PopScope() => m_lexicalScopes.Pop();

    public LayeIrBasicBlockBuilder AppendBasicBlock()
    {
        var block = new LayeIrBasicBlockBuilder(this);
        m_basicBlocks.Add(block);
        return block;
    }

    public void PositionAtEnd(LayeIrBasicBlockBuilder builder)
    {
        CurrentBlock = builder;
        m_index = CurrentBlock.InstructionCount;
    }

    public LayeIr.Function Build()
    {
        var blocks = m_basicBlocks.Select(b => b.Build()).ToArray();
        return new LayeIr.Function(m_name, m_symbol, blocks);
    }

    private void AddInstruction(LayeIr.Insn insn)
    {
        CurrentBlock.InsertInstruction(m_index, insn);
        m_index++;
    }

    public TValue BuildExpression<TValue>(TValue value)
        where TValue : LayeIr.Value
    {
        AddInstruction(value);
        return value;
    }

    public LayeIr.Integer BuildInteger(LayeToken.Integer token, SymbolType type) => BuildInteger(token.SourceSpan, token.LiteralValue, type);
    public LayeIr.Integer BuildInteger(SourceSpan sourceSpan, ulong literalValue, SymbolType type) => BuildExpression(new LayeIr.Integer(sourceSpan, literalValue, type));
    public LayeIr.Float BuildFloat(LayeToken.Float token, SymbolType type) => BuildFloat(token.SourceSpan, token.LiteralValue, type);
    public LayeIr.Float BuildFloat(SourceSpan sourceSpan, double literalValue, SymbolType type) => BuildExpression(new LayeIr.Float(sourceSpan, literalValue, type));
    public LayeIr.String BuildString(LayeToken.String token, SymbolType type) => BuildString(token.SourceSpan, token.LiteralValue, type);
    public LayeIr.String BuildString(SourceSpan sourceSpan, string literalValue, SymbolType type) => BuildExpression(new LayeIr.String(sourceSpan, literalValue, type));

    public LayeIr.InvokeGlobalFunction BuildInvokeGlobalFunction(SourceSpan sourceSpan, Symbol.Function fnSymbol, LayeIr.Value[] argValues)
        => BuildExpression(new LayeIr.InvokeGlobalFunction(sourceSpan, fnSymbol, argValues));

    public LayeIr.IntToRawPtrCast BuildIntToRawPtrCast(LayeIr.Value value) => BuildExpression(new LayeIr.IntToRawPtrCast(value.SourceSpan, value));
}

internal sealed class LayeIrBasicBlockBuilder
{
    public LayeIrFunctionBuilder FunctionBuilder { get; }

    private readonly List<LayeIr.Insn> m_instructions = new();

    public int InstructionCount => m_instructions.Count;
    public LayeIr.BranchInsn? TerminatorInstruction { get; set; } = default;

    public LayeIrBasicBlockBuilder(LayeIrFunctionBuilder functionBuilder)
    {
        FunctionBuilder = functionBuilder;
    }

    public LayeIr.BasicBlock Build()
    {
        if (TerminatorInstruction is null)
            throw new InvalidOperationException("Basic block builder was not terminated with a branch instruction");

        var insns = m_instructions.ToArray();
        var branch = TerminatorInstruction;

        return new LayeIr.BasicBlock(insns, branch);
    }

    public void InsertInstruction(int index, LayeIr.Insn insn)
    {
        m_instructions.Insert(index, insn);
    }
}
