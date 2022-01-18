#define ALLOW_RADIX_INTEGERS

namespace laye.Compiler;

internal sealed class LayeLexer
{
    public static LayeToken[] ReadTokensFromFile(string filePath, List<Diagnostic> diagnostics)
    {
        string sourceText = File.ReadAllText(filePath);
        var lexer = new LayeLexer(filePath, diagnostics, sourceText);
        return lexer.ReadTokens();
    }

    private readonly string m_fileName;
    private readonly List<Diagnostic> m_diagnostics;
    private readonly string m_sourceText;

    private uint m_position = 0;
    private uint m_line = 1, m_column = 1;

    private SourceLocation CurrentLocation => new(m_fileName, m_position, m_line, m_column);

    private char CurrentChar => IsEoF ? '\0' : m_sourceText[(int)m_position];
    private char NextChar => HasNext ? m_sourceText[(int)m_position + 1] : '\0';

    private bool IsEoF => m_position >= m_sourceText.Length;
    private bool HasNext => m_position + 1 >= m_sourceText.Length;

    public LayeLexer(string fileName, List<Diagnostic> diagnostics, string sourceText)
    {
        m_fileName = fileName;
        m_diagnostics = diagnostics;
        m_sourceText = sourceText;
    }

    private LayeToken[] ReadTokens()
    {
        var tokens = new List<LayeToken>();

        while (!IsEoF)
        {
            var token = ReadToken();
            if (token is null)
                break;

            ConsumeTriviaUntilLineEnd();
            tokens.Add(token);
        }

        return tokens.ToArray();
    }

    private void Advance()
    {
        m_position++;
    }

    private void ConsumeTrivia()
    {
        uint position = m_position;
        while (true)
        {
            ConsumeTriviaUntilLineEnd();
            if (m_position == position)
                return;

            position = m_position;
        }
    }

    private void ConsumeTriviaUntilLineEnd()
    {
        while (!IsEoF)
        {
            char c = CurrentChar;
            if (char.IsWhiteSpace(c))
            {
                Advance();
                if (c == '\n')
                    break;
            }
            else if (c == '/')
            {
                if (HasNext && NextChar == '/')
                {
                    Advance();
                    Advance();

                    while (!IsEoF)
                    {
                        Advance();
                        if (c == '\n')
                            break;
                    }

                    break;
                }
            }
            else break;
        }
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

#if ALLOW_RADIX_INTEGERS
    private static bool IsDigitInRadix(char c, int radix) => DigitValueInRadix(c, radix) >= 0;
    private static int DigitValueInRadix(char c, int radix)
    {
        if (c >= '0' && c <= '9' && (c - '0') < radix)
            return c - '0';

        c = char.ToLower(c);
        if (c >= 'a' && c <= 'z' && (c - '0' + 10) < radix)
            return c - '0' + 10;

        return -1;
    }
#endif

    private LayeToken? ReadToken()
    {
        ConsumeTrivia();

        var startLocation = CurrentLocation;

        switch (CurrentChar)
        {
            case '(':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.OpenParen);

            case ')':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.CloseParen);

            case '[':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.OpenBracket);

            case ']':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.CloseBracket);

            case '{':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.OpenBrace);

            case '}':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.CloseBrace);

            case ',':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.Comma);

            case '.':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.Dot);

            case '?':
                Advance();
                if (CurrentChar == '.')
                {
                    Advance();
                    return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.QuestionDot);
                }

                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.Question);

            case ':':
                Advance();
                if (CurrentChar == ':')
                {
                    Advance();
                    return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.PathSeparator);
                }

                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.Colon);

            case ';':
                Advance();
                return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.SemiColon);

            case '=':
                Advance();
                if (CurrentChar == '>')
                {
                    Advance();
                    return new LayeToken.Delimiter(new(startLocation, CurrentLocation), Delimiter.FatArrow);
                }
                else if (CurrentChar == '=')
                {
                    Advance();
                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareEqual);
                }

                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Assign);

            case '+':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Add);

            case '-':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Subtract);

            case '*':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Multiply);

            case '/':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Divide);

            case '%':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Modulo);

            case '&':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.BitAnd);

            case '|':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.BitOr);

            case '^':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.BitXor);

            case '~':
                Advance();
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.BitNot);

            case '!':
                if (NextChar == '=')
                {
                    Advance();
                    Advance();

                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareNotEqual);
                }

                goto default;

            case '<':
                Advance();
                if (CurrentChar == '<')
                {
                    Advance();
                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.LeftShift);
                }
                else if (CurrentChar == '=')
                {
                    Advance();
                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareLessThanOrEqual);
                }

                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareLessThan);

            case '>':
                Advance();
                if (CurrentChar == '>')
                {
                    Advance();
                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.RightShift);
                }
                else if (CurrentChar == '=')
                {
                    Advance();
                    return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareGreaterThanOrEqual);
                }

                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.CompareGreaterThan);

            case '"': return ReadStringLiteral();
            case '\'': return ReadCharacterLiteral();

            default:
                if (IsIdentifierChar(CurrentChar))
                    return ReadIdentifierOrNumber();

                m_diagnostics.Add(new Diagnostic.Error(new(startLocation, startLocation), "invalid character in source file"));
                return null;
        }
    }

    private LayeToken? ReadIdentifierOrNumber()
    {
        if (!char.IsDigit(CurrentChar))
            return ReadIdentifier();

        var startLocation = CurrentLocation;
        while (!IsEoF)
        {
            if (!char.IsDigit(CurrentChar) && IsIdentifierChar(CurrentChar))
            {
                m_position = startLocation.SourceIndex;
                return ReadIdentifier();
            }

            if (CurrentChar == '.' && char.IsDigit(NextChar))
            {
                Advance(); // `.`

                var fractionalStartLocation = CurrentLocation;
                while (char.IsDigit(CurrentChar))
                    Advance();

                var floatSpan = new SourceSpan(startLocation, CurrentLocation);
                double floatValue = double.Parse(floatSpan.ToString(m_sourceText));

                return new LayeToken.Float(floatSpan, floatValue);
            }

#if ALLOW_RADIX_INTEGERS
            if (CurrentChar == '#')
            {
                int radix = int.Parse(new SourceSpan(startLocation, CurrentLocation).ToString(m_sourceText));
                if (!IsDigitInRadix(NextChar, radix))
                    break;

                Advance(); // `#`

                ulong rintValue = 0;

                var fractionalStartLocation = CurrentLocation;
                while (IsDigitInRadix(CurrentChar, radix))
                {
                    int cValue = DigitValueInRadix(CurrentChar, radix);
                    Advance();
                }

                var rintSpan = new SourceSpan(startLocation, CurrentLocation);
                return new LayeToken.Integer(rintSpan, rintValue);
            }
#endif

            if (!char.IsDigit(CurrentChar))
                break;

            Advance();
        }

        var intSpan = new SourceSpan(startLocation, CurrentLocation);
        ulong intValue = ulong.Parse(intSpan.ToString(m_sourceText));

        return new LayeToken.Integer(intSpan, intValue);
    }

    private LayeToken? ReadIdentifier()
    {
        var startLocation = CurrentLocation;
        while (!IsEoF && IsIdentifierChar(CurrentChar))
            Advance();

        var span = new SourceSpan(startLocation, CurrentLocation);
        string identifier = span.ToString(m_sourceText);

        // TODO(local): pick out keywords from identifiers plz

        return new LayeToken.Identifier(span, identifier);
    }

    private LayeToken? ReadStringLiteral()
    {
        var startLocation = CurrentLocation;
        Advance();

        // TODO(local): strings do not support escapes. it might be fun to keep it this way and implement escapes from scratch

        var literalStartLocation = CurrentLocation;
        while (CurrentChar != '"' && CurrentChar != '\n')
            Advance();

        if (CurrentChar != '"')
        {
            m_diagnostics.Add(new Diagnostic.Error(new(startLocation, CurrentLocation), "unfinished string literal"));
            return null;
        }

        var literalEndLocation = CurrentLocation;
        string literalValue = new SourceSpan(literalStartLocation, literalEndLocation).ToString(m_sourceText);

        Advance();
        var totalSpan = new SourceSpan(startLocation, CurrentLocation);

        return new LayeToken.String(totalSpan, literalValue);
    }

    private LayeToken? ReadCharacterLiteral()
    {
        var startLocation = CurrentLocation;
        Advance();

        // TODO(local): characters do not support escapes. it might be fun to keep it this way and implement escapes from scratch

        char c = CurrentChar;
        if (c == '\'')
        {
            m_diagnostics.Add(new Diagnostic.Error(new(startLocation, CurrentLocation), "empty character literal"));
            return null;
        }

        if (CurrentChar != '\'')
        {
            m_diagnostics.Add(new Diagnostic.Error(new(startLocation, CurrentLocation), "unfinished character literal"));
            return null;
        }
        Advance();
        var totalSpan = new SourceSpan(startLocation, CurrentLocation);

        return new LayeToken.Character(totalSpan, c);
    }
}
