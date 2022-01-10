namespace laye.Compiler;

internal sealed class LayeParser
{
    private readonly List<Diagnostic> m_diagnostics;
    private readonly Token[] m_tokens;

    private int m_tokenIndex = 0;

    private bool IsEoF => m_tokenIndex >= m_tokens.Length;

    private Token CurrentToken => Peek(0);
    private Token NextToken => Peek(1);

    public LayeParser(List<Diagnostic> diagnostics, Token[] tokens)
    {
        m_diagnostics = diagnostics;
        m_tokens = tokens;
    }

    public AstNode[] GetSyntaxTree()
    {
        var topLevelNodes = new List<AstNode>();

        return topLevelNodes.ToArray();
    }

    #region Token Traversal

    private void Advance() => m_tokenIndex++;
    private Token Peek(int peekOffset = 1)
    {
        if (IsEoF)
            throw new InvalidOperationException("Cannot peek token: end of file reached");

        int tokenIndex = m_tokenIndex + peekOffset;
        if (tokenIndex < 0 || tokenIndex >= m_tokens.Length)
            throw new InvalidOperationException($"Cannot peek token using offset {peekOffset}: token index out of range");

        return m_tokens[tokenIndex];
    }

    private bool Check<TToken>()
        where TToken : Token => CurrentToken is TToken;
    private bool Check<TToken>(out TToken token)
        where TToken : Token
    {
        if (CurrentToken is TToken current)
        {
            token = current;
            return true;
        }

        token = null!;
        return false;
    }

    private bool CheckIdentifier(out Token.Identifier identifier) => Check(out identifier);
    private bool CheckDelimiter(Delimiter kind, out Token.Delimiter delimiter) => Check(out delimiter) && delimiter.Kind == kind;

    #endregion

    #region Modifiers

    private bool TryGetModifierList(ModifierContext context, out AstNode.Modifier[] modifiers)
    {
        int startIndex = 0;

        while (!IsEoF && Check<Token.Keyword>(out var keyword))
        {
        }

        modifiers = Array.Empty<AstNode.Modifier>();
        return true;
    }

    #endregion

    #region Types

    private AstNode.Type? TryGetTypeNode()
    {
        return null;
    }

    private AstNode.Type? TryGetTypeNode_Impl()
    {


        return null;
    }

    #endregion
}
