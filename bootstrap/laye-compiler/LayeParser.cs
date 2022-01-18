#define ALLOW_NAMESPACING

namespace laye.Compiler;

public sealed class LayeParser
{
    public static LayeAst[] ParseSyntaxFromFile(string sourceFilePath, List<Diagnostic> diagnostics)
    {
        var tokens = LayeLexer.ReadTokensFromFile(sourceFilePath, diagnostics);
        if (tokens.Length == 0)
            return Array.Empty<LayeAst>();

        var parser = new LayeParser(sourceFilePath, diagnostics, tokens);
        return parser.GetSyntaxTree();
    }

    private readonly string m_fileName;
    private readonly List<Diagnostic> m_diagnostics;
    private readonly LayeToken[] m_tokens;

    private int m_tokenIndex = 0;

    private bool IsEoF => m_tokenIndex >= m_tokens.Length;

    private LayeToken CurrentToken => Peek(0);
    private LayeToken NextToken => Peek(1);

    private SourceSpan MostRecentTokenSpan => IsEoF ? m_tokens[m_tokens.Length - 1].SourceSpan : CurrentToken.SourceSpan;

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

    private bool Check<TToken>() where TToken : LayeToken => CurrentToken is TToken;
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
    private bool CheckOperator(Operator kind, out LayeToken.Operator @operator) => Check(out @operator) && @operator.Kind == kind;
    private bool CheckKeyword(Keyword kind, out LayeToken.Keyword keyword) => Check(out keyword) && keyword.Kind == kind;

    private bool Expect<TToken>()
        where TToken : LayeToken
    {
        if (Check<TToken>())
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Expect<TToken>(out TToken token, Predicate<TToken>? condition = null)
        where TToken : LayeToken
    {
        if (Check(out token) && (condition?.Invoke(token) ?? true))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool ExpectIdentifier(out LayeToken.Identifier identifier) => Expect(out identifier);
    private bool ExpectDelimiter(Delimiter kind, out LayeToken.Delimiter delimiter) => Expect(out delimiter, delimiter => delimiter.Kind == kind);
    private bool ExpectOperator(Operator kind, out LayeToken.Operator @operator) => Expect(out @operator, @operator => @operator.Kind == kind);
    private bool ExpectKeyword(Keyword kind, out LayeToken.Keyword keyword) => Expect(out keyword, keyword => keyword.Kind == kind);

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

        //var typeModifiers = GetModifierList();
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
        else if (CheckKeyword(Keyword.Bool, out var boolKw))
        {
            type = new LayeAst.BuiltInType(boolKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.RawPtr, out var rawptrKw))
        {
            type = new LayeAst.BuiltInType(rawptrKw);
            Advance();
        }
        else if (CheckKeyword(Keyword.NoReturn, out var noreturnKw))
        {
            type = new LayeAst.BuiltInType(noreturnKw);
            Advance();
        }
        else if (CheckIdentifier(out var nameLookupId))
        {
            LayeAst.PathPart pathPart = new LayeAst.NamePathPart(nameLookupId);
            Advance();

#if ALLOW_NAMESPACING
            if (CheckDelimiter(Delimiter.PathSeparator, out _))
            {
                while (CheckDelimiter(Delimiter.PathSeparator, out var pathSep))
                {
                    Advance();
                    if (!ExpectIdentifier(out var nextNameId))
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "identifier expected to continue path syntax when trying to parse type"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    pathPart = new LayeAst.JoinedPath(pathPart, pathSep, new LayeAst.NamePathPart(nextNameId));
                }
            }
#endif

            type = new LayeAst.NamedType(pathPart);
        }

        if (type is null)
        {
            if (isRequired)
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "failed to parse type"));

            m_tokenIndex = tokenIndex;
            return null;
        }

        while (!IsEoF)
        {
            var containerModifiers = GetModifierList();
            if (CheckOperator(Operator.Multiply, out var starOp))
            {
                type = new LayeAst.PointerType(type, containerModifiers, starOp);
                Advance();
            }
        }

        return type;
    }

    #endregion
}
