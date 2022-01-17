using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    private int m_position = 0;

    private char CurrentChar => m_sourceText[m_position];
    private char NextChar => m_sourceText[m_position + 1];

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

            tokens.Add(token);
        }

        return tokens.ToArray();
    }

    private void Advance()
    {
        m_position++;
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

    private LayeToken? ReadToken()
    {
        ConsumeTriviaUntilLineEnd();
        return null;
    }
}
