namespace laye;

internal sealed record class LayeIrModule(LayeIr.Function[] Functions, Symbol[] TypeSymbols);

internal abstract record class LayeIr(SourceSpan SourceSpan) : IHasSourceSpan
{
    public sealed record class Identifier(string Value, SourceSpan SourceSpan) : LayeIr(SourceSpan);

    #region Declarations

    public abstract record class Decl(SourceSpan SourceSpan) : LayeIr(SourceSpan);

    public sealed record class Function(Identifier Name) : Decl(Name.SourceSpan);

    #endregion

    #region Instructions

    public sealed record class BasicBlock(Insn[] Instructions, BranchInsn BranchInstruction);

    public abstract record class Insn(SourceSpan SourceSpan) : LayeIr(SourceSpan);
    public abstract record class BranchInsn(SourceSpan SourceSpan) : LayeIr(SourceSpan);

    #endregion
}
