namespace laye.Compiler;

public sealed class LayeParser
{
    public static LayeAst[] ParseSyntaxFromFile(string sourceFilePath, List<Diagnostic> diagnostics)
    {
        var parser = new LayeParser(sourceFilePath, diagnostics, LayeLexer.ReadTokensFromFile(sourceFilePath, diagnostics));
        return parser.GetSyntaxTree();
    }

    private readonly string m_fileName;
    private readonly List<Diagnostic> m_diagnostics;
    private readonly LayeToken[] m_tokens;

    private int m_tokenIndex = 0;

    private bool IsEoF => m_tokenIndex >= m_tokens.Length;

    private LayeToken CurrentToken => Peek(0);
    private LayeToken NextToken => Peek(1);

    private LayeParser(string fileName, List<Diagnostic> diagnostics, LayeToken[] tokens)
    {
        m_fileName = fileName;
        m_diagnostics = diagnostics;
        m_tokens = tokens;
    }

    private LayeAst[] GetSyntaxTree()
    {
        var topLevelNodes = new List<LayeAst>();

        return topLevelNodes.ToArray();
    }

    #region Token Traversal

    private void Advance() => m_tokenIndex++;
    private LayeToken Peek(int peekOffset = 1)
    {
        if (IsEoF)
            throw new InvalidOperationException("Cannot peek token: end of file reached");

        int tokenIndex = m_tokenIndex + peekOffset;
        if (tokenIndex < 0 || tokenIndex >= m_tokens.Length)
            throw new InvalidOperationException($"Cannot peek token using offset {peekOffset}: token index out of range");

        return m_tokens[tokenIndex];
    }

    private bool Check<TToken>()
        where TToken : LayeToken => CurrentToken is TToken;
    private bool Check<TToken>(out TToken token)
        where TToken : LayeToken
    {
        if (CurrentToken is TToken current)
        {
            token = current;
            return true;
        }

        token = null!;
        return false;
    }

    private bool CheckIdentifier(out LayeToken.Identifier identifier) => Check(out identifier);
    private bool CheckDelimiter(Delimiter kind, out LayeToken.Delimiter delimiter) => Check(out delimiter) && delimiter.Kind == kind;
    private bool CheckKeyword(Keyword kind, out LayeToken.Keyword keyword) => Check(out keyword) && keyword.Kind == kind;

    #endregion

    #region Modifiers

    private LayeAst.Modifier[] GetModifierList()
    {
        var result = new List<LayeAst.Modifier>();
        while (!IsEoF && Check<LayeToken.Keyword>(out var keyword) && keyword.IsModifier())
        {
            result.Add(keyword.ToModifierNode());
            Advance(); // modifier keyword
        }

        return result.ToArray();
    }

    #endregion

    #region Types

    private LayeAst.Type? TryGetTypeNode(bool isRequired = true)
    {
        int tokenIndex = m_tokenIndex;

        var typeModifiers = GetModifierList();

        LayeAst.Type? type = null;

        if (CheckKeyword(Keyword.SizedInt, out var sizedIntKw))
        {
            type = new LayeAst.BuiltInType(sizedIntKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.SizedUInt, out var sizedUIntKw))
        {
            type = new LayeAst.BuiltInType(sizedUIntKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.SizedFloat, out var sizedFloatKw))
        {
            type = new LayeAst.BuiltInType(sizedFloatKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.Int, out var intKw))
        {
            type = new LayeAst.BuiltInType(intKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.UInt, out var uintKw))
        {
            type = new LayeAst.BuiltInType(uintKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.Float, out var floatKw))
        {
            type = new LayeAst.BuiltInType(floatKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.Void, out var voidKw))
        {
            type = new LayeAst.BuiltInType(voidKw);
            Advance();
        }

        return type;
    }

    #endregion
}
