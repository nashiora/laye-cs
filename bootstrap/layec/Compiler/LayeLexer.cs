#define ALLOW_RADIX_INTEGERS
#define ALLOW_ARBITRARY_SIZED_PRIMITIVES

namespace laye.Compiler;

internal sealed class LayeLexer
{
    public static bool ReadTokensFromFile(string filePath, List<Diagnostic> diagnostics, out LayeToken[] result)
    {
        string sourceText = File.ReadAllText(filePath);
        var lexer = new LayeLexer(filePath, diagnostics, sourceText);
        return lexer.ReadTokens(out result);
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
    private bool HasNext => m_position + 1 < m_sourceText.Length;

    public LayeLexer(string fileName, List<Diagnostic> diagnostics, string sourceText)
    {
        m_fileName = fileName;
        m_diagnostics = diagnostics;
        m_sourceText = sourceText;
    }

    private bool ReadTokens(out LayeToken[] result)
    {
        var tokens = new List<LayeToken>();
        result = Array.Empty<LayeToken>();

        while (!IsEoF)
        {
            var token = ReadToken();
            if (token is null)
            {
                if (m_diagnostics.Any(d => d is Diagnostic.Error))
                    return false;
                else break;
            }

            ConsumeTriviaUntilLineEnd();
            tokens.Add(token);
        }

        result = tokens.ToArray();
        return true;
    }

    private void Advance()
    {
        char c = CurrentChar;
        m_position++;

        if (c == '\n')
        {
            m_line++;
            m_column = 1;
        }
        else m_column++;
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
            else if (c == '/' && HasNext && NextChar == '/')
            {
                Advance();
                Advance();

                while (!IsEoF)
                {
                    c = CurrentChar;
                    Advance();
                    if (c == '\n')
                        break;
                }

                break;
            }
            // TODO(local): nested blocks
            else if (c == '/' && HasNext && NextChar == '*')
            {
                Advance();
                Advance();

                while (!IsEoF)
                {
                    char p = c;
                    c = CurrentChar;
                    Advance();
                    if (c == '/' && p == '*')
                        break;
                }

                break;
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
        if (IsEoF)
            return null;

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
                return new LayeToken.Operator(new(startLocation, CurrentLocation), Operator.Remainder);

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
        switch (identifier)
        {
            case "int": return new LayeToken.Keyword(span, Keyword.Int);
            case "i8": return new LayeToken.Keyword(span, Keyword.SizedInt, 8);
            case "i16": return new LayeToken.Keyword(span, Keyword.SizedInt, 16);
            case "i32": return new LayeToken.Keyword(span, Keyword.SizedInt, 32);
            case "i64": return new LayeToken.Keyword(span, Keyword.SizedInt, 64);
            case "i128": return new LayeToken.Keyword(span, Keyword.SizedInt, 128);

            case "uint": return new LayeToken.Keyword(span, Keyword.UInt);
            case "u8": return new LayeToken.Keyword(span, Keyword.SizedUInt, 8);
            case "u16": return new LayeToken.Keyword(span, Keyword.SizedUInt, 16);
            case "u32": return new LayeToken.Keyword(span, Keyword.SizedUInt, 32);
            case "u64": return new LayeToken.Keyword(span, Keyword.SizedUInt, 64);
            case "u128": return new LayeToken.Keyword(span, Keyword.SizedUInt, 128);

            case "float": return new LayeToken.Keyword(span, Keyword.Float);
            case "f32": return new LayeToken.Keyword(span, Keyword.SizedFloat, 32);
            case "f64": return new LayeToken.Keyword(span, Keyword.SizedFloat, 64);

            case "void": return new LayeToken.Keyword(span, Keyword.Void);
            case "bool": return new LayeToken.Keyword(span, Keyword.Bool);
            case "rune": return new LayeToken.Keyword(span, Keyword.Rune);

            case "rawptr": return new LayeToken.Keyword(span, Keyword.RawPtr);
            case "string": return new LayeToken.Keyword(span, Keyword.String);
            case "noreturn": return new LayeToken.Keyword(span, Keyword.NoReturn);
            case "dynamic": return new LayeToken.Keyword(span, Keyword.Dynamic);

            case "struct": return new LayeToken.Keyword(span, Keyword.Struct);
            case "enum": return new LayeToken.Keyword(span, Keyword.Enum);

            case "varargs": return new LayeToken.Keyword(span, Keyword.VarArgs);

            case "true": return new LayeToken.Keyword(span, Keyword.True);
            case "false": return new LayeToken.Keyword(span, Keyword.False);

            case "nullptr": return new LayeToken.Keyword(span, Keyword.NullPtr);
            case "nil": return new LayeToken.Keyword(span, Keyword.Nil);

            case "context": return new LayeToken.Keyword(span, Keyword.Context);
            case "global": return new LayeToken.Keyword(span, Keyword.Global);

            case "noinit": return new LayeToken.Keyword(span, Keyword.NoInit);

            case "public": return new LayeToken.Keyword(span, Keyword.Public);
            case "internal": return new LayeToken.Keyword(span, Keyword.Internal);
            case "private": return new LayeToken.Keyword(span, Keyword.Private);
            case "nocontext": return new LayeToken.Keyword(span, Keyword.NoContext);
            case "cdecl": return new LayeToken.Keyword(span, Keyword.CDecl);
            case "fastcall": return new LayeToken.Keyword(span, Keyword.FastCall);
            case "stdcall": return new LayeToken.Keyword(span, Keyword.StdCall);
            case "intrinsic": return new LayeToken.Keyword(span, Keyword.Intrinsic);
            case "export": return new LayeToken.Keyword(span, Keyword.Export);
            case "extern": return new LayeToken.Keyword(span, Keyword.Extern);
            case "inline": return new LayeToken.Keyword(span, Keyword.Inline);
            case "naked": return new LayeToken.Keyword(span, Keyword.Naked);
            case "const": return new LayeToken.Keyword(span, Keyword.Const);
            case "readonly": return new LayeToken.Keyword(span, Keyword.ReadOnly);
            case "writeonly": return new LayeToken.Keyword(span, Keyword.WriteOnly);

            case "and": return new LayeToken.Keyword(span, Keyword.And);
            case "or": return new LayeToken.Keyword(span, Keyword.Or);
            case "xor": return new LayeToken.Keyword(span, Keyword.Xor);
            case "not": return new LayeToken.Keyword(span, Keyword.Not);
            case "cast": return new LayeToken.Keyword(span, Keyword.Cast);
            case "sizeof": return new LayeToken.Keyword(span, Keyword.SizeOf);
            case "offsetof": return new LayeToken.Keyword(span, Keyword.OffsetOf);

            case "if": return new LayeToken.Keyword(span, Keyword.If);
            case "else": return new LayeToken.Keyword(span, Keyword.Else);
            case "while": return new LayeToken.Keyword(span, Keyword.While);
            case "for": return new LayeToken.Keyword(span, Keyword.For);
            case "switch": return new LayeToken.Keyword(span, Keyword.Switch);
            case "case": return new LayeToken.Keyword(span, Keyword.Case);
            case "default": return new LayeToken.Keyword(span, Keyword.Default);
            case "return": return new LayeToken.Keyword(span, Keyword.Return);
            case "break": return new LayeToken.Keyword(span, Keyword.Break);
            case "continue": return new LayeToken.Keyword(span, Keyword.Continue);
            case "yield": return new LayeToken.Keyword(span, Keyword.Yield);

            default:
#if ALLOW_ARBITRARY_SIZED_PRIMITIVES
                if (identifier[0] == 'i' || identifier[0] == 'u' || identifier[0] == 'f')
                {
                    if (!identifier.Skip(1).Any(c => !char.IsDigit(c)))
                    {
                        var kw = identifier[0] == 'i' ? Keyword.SizedInt : identifier[0] == 'u' ? Keyword.SizedUInt : Keyword.SizedFloat;
                        return new LayeToken.Keyword(span, kw, uint.Parse(identifier.AsSpan()[1..]));
                    }
                }
#endif

                return new LayeToken.Identifier(span, identifier);
        }
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
