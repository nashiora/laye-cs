#define ALLOW_NAMESPACING

using System.Diagnostics;

namespace laye.Compiler;

internal sealed class LayeParser
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

        while (!IsEoF)
        {
            var topLevelNode = ReadTopLevel();
            if (topLevelNode is null)
            {
                AssertHasErrors("failing to get top level node in main parse loop");
                return Array.Empty<LayeAst>();
            }

            topLevelNodes.Add(topLevelNode);
        }

        return topLevelNodes.ToArray();
    }

    private void AssertHasErrors(string context)
    {
        Debug.Assert(m_diagnostics.Any(d => d is Diagnostic.Error), $"No error diagnostics generated when {context}");
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
    private bool CheckDelimiter(Delimiter kind) => Check(out LayeToken.Delimiter delimiter) && delimiter.Kind == kind;
    private bool CheckDelimiter(Delimiter kind, out LayeToken.Delimiter delimiter) => Check(out delimiter) && delimiter.Kind == kind;
    private bool CheckOperator(Operator kind, out LayeToken.Operator @operator) => Check(out @operator) && @operator.Kind == kind;
    private bool CheckOperator(out LayeToken.Operator @operator, Predicate<LayeToken.Operator> predicate) => Check(out @operator) && predicate(@operator);
    private bool CheckKeyword(Keyword kind) => Check(out LayeToken.Keyword keyword) && keyword.Kind == kind;
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

    private LayeAst.ContainerModifiers? ReadContainerModifierList(bool isRequired = true)
    {
        var modifiers = new LayeAst.ContainerModifiers();

        while (Check<LayeToken.Keyword>(out var keyword))
        {
            switch (keyword.Kind)
            {
                case Keyword.ReadOnly: case Keyword.WriteOnly: case Keyword.Const:
                {
                    if (modifiers.AccessModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one access modifier allowed per container type"));
                        return null;
                    }

                    Advance(); // keyword
                    modifiers.AccessModifier = new(keyword);
                } break;

                default: goto exit;
            }
        }

    exit:
        return modifiers;
    }

    private LayeAst.FunctionModifiers? ReadFunctionModifierList(bool isRequired = true)
    {
        int tokenIndex = m_tokenIndex;

        var modifiers = new LayeAst.FunctionModifiers();
        while (Check<LayeToken.Keyword>(out var keyword))
        {
            switch (keyword.Kind)
            {
                case Keyword.Public: case Keyword.Internal: case Keyword.Private:
                {
                    if (modifiers.VisibilityModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one visibility modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    Advance(); // keyword
                    modifiers.VisibilityModifier = new(keyword);
                } break;

                case Keyword.Intrinsic: case Keyword.Export:
                case Keyword.Inline:    case Keyword.Naked:
                {
                    if (modifiers.FunctionHintModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one function hint modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    if (modifiers.ExternModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one of either function hint modifier and extern modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    Advance(); // keyword
                    modifiers.FunctionHintModifier = new(keyword);
                } break;

                case Keyword.Extern:
                {
                    if (modifiers.ExternModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one extern modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    if (modifiers.FunctionHintModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one of either function hint modifier and extern modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    Advance(); // keyword
                    if (!Expect<LayeToken.String>(out var externLibraryName))
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected string as extern library name"));

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    modifiers.ExternModifier = new(keyword, externLibraryName);
                } break;

                default: goto exit;
            }
        }

    exit:
        return modifiers;
    }

    private bool ReadCallingConventionInto(LayeAst.FunctionModifiers modifiers, bool isRequired = true)
    {
        int tokenIndex = m_tokenIndex;

        while (Check<LayeToken.Keyword>(out var keyword))
        {
            switch (keyword.Kind)
            {
                case Keyword.NoContext: case Keyword.CDecl:
                case Keyword.FastCall:  case Keyword.StdCall:
                {
                    if (modifiers.CallingConventionModifier is not null)
                    {
                        if (isRequired)
                            m_diagnostics.Add(new Diagnostic.Error(keyword.SourceSpan, "only one calling convention modifier allowed per function declaration"));

                        m_tokenIndex = tokenIndex;
                        return false;
                    }

                    Advance(); // keyword
                    modifiers.CallingConventionModifier = new(keyword);
                } break;

                default: goto exit;
            }
        }

    exit:
        return true;
    }

#endregion

#region Types

    private LayeAst.Type? TryReadTypeNode(bool isRequired = true)
    {
        int tokenIndex = m_tokenIndex;

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
        else if (CheckKeyword(Keyword.String, out var stringKw))
        {
            type = new LayeAst.BuiltInType(stringKw);
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
            int suffixTokenIndex = m_tokenIndex;

            if (ReadContainerModifierList(isRequired) is not { } containerModifiers)
            {
                if (isRequired)
                    AssertHasErrors("failing to parse type modifiers");
                return null;
            }

            if (!containerModifiers.IsEmpty)
            {
                var containerType = ReadContainerTypeSuffix(type, containerModifiers, isRequired);
                if (containerType is null)
                {
                    AssertHasErrors("reading container type suffix");
                    return null;
                }

                type = containerType;
                continue;
            }

            var functionModifiers = new LayeAst.FunctionModifiers();
            if (!ReadCallingConventionInto(functionModifiers, isRequired))
            {
                if (isRequired)
                    AssertHasErrors("reading potential function modifier list");

                m_tokenIndex = tokenIndex;
                return null;
            }

            if (!functionModifiers.IsEmpty)
            {
                if (CheckIdentifier(out var _))
                {
                    m_tokenIndex = suffixTokenIndex;
                    break;
                }

                var functionType = ReadFunctionTypeSuffix(type, functionModifiers, isRequired);
                if (functionType is null)
                {
                    AssertHasErrors("reading function type suffix");
                    return null;
                }

                type = functionType;
                continue;
            }

            LayeAst.Type? nextType;
            
            nextType = ReadContainerTypeSuffix(type, new(), false);
            if (nextType is not null)
            {
                type = nextType;
                continue;
            }

            nextType = ReadFunctionTypeSuffix(type, new(), false);
            if (nextType is not null)
            {
                type = nextType;
                continue;
            }

            break;

            LayeAst.Type? ReadContainerTypeSuffix(LayeAst.Type type, LayeAst.ContainerModifiers modifiers, bool isRequired = true)
            {
                int tokenIndex = m_tokenIndex;

                if (CheckOperator(Operator.Multiply, out var starOp))
                {
                    Advance(); // `*`
                    return new LayeAst.PointerType(type, modifiers, starOp);
                }
                else if (CheckDelimiter(Delimiter.OpenBracket, out var openBracketDelim))
                {
                    Advance(); // `[`

                    if (CheckDelimiter(Delimiter.CloseBracket, out var closeBracketDelim))
                    {
                        Advance(); // `]`
                        return new LayeAst.SliceType(type, modifiers, openBracketDelim, closeBracketDelim);
                    }
                    else if (CheckOperator(Operator.Multiply, out var starOp2))
                    {
                        Advance(); // `*`

                        if (!ExpectDelimiter(Delimiter.CloseBracket, out var closeBracketDelim2))
                        {
                            if (isRequired)
                                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `]` to close buffer type"));

                            m_tokenIndex = tokenIndex;
                            return null;
                        }

                        return new LayeAst.BufferType(type, modifiers, openBracketDelim, starOp2, closeBracketDelim2);
                    }
                }

                if (isRequired)
                    m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected container type syntax after container type modifiers"));

                m_tokenIndex = tokenIndex;
                return null;
            }

            LayeAst.Type? ReadFunctionTypeSuffix(LayeAst.Type type, LayeAst.FunctionModifiers modifiers, bool isRequired = true)
            {
                int tokenIndex = m_tokenIndex;

                if (!ExpectDelimiter(Delimiter.OpenParen, out var _))
                {
                    if (isRequired)
                        m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `(` for function type syntax"));

                    m_tokenIndex = tokenIndex;
                    return null;
                }

                var paramTypes = new List<LayeAst.Type>();
                var varArgsKind = VarArgsKind.None;

                while (!IsEoF && !CheckDelimiter(Delimiter.CloseParen))
                {
                    var paramType = TryReadTypeNode(isRequired);
                    if (paramType is null)
                    {
                        if (isRequired)
                            AssertHasErrors("reading function parameter type");

                        m_tokenIndex = tokenIndex;
                        return null;
                    }

                    paramTypes.Add(paramType);

                    if (!CheckDelimiter(Delimiter.Comma))
                        break;
                    else Advance(); // `,`
                }

                if (!ExpectDelimiter(Delimiter.CloseParen, out var _))
                {
                    if (isRequired)
                        m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `)` for function type syntax"));

                    m_tokenIndex = tokenIndex;
                    return null;
                }

                return new LayeAst.FunctionType(modifiers, type, paramTypes.ToArray(), varArgsKind);
            }
        }

        return type;
    }

#endregion

    private LayeAst? ReadTopLevel()
    {
        if (CheckKeyword(Keyword.Struct))
            return ReadStructDeclaration();

        return ReadFunctionDeclaration();

        //m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "unexpected token at top level"));
        //return null;
    }

    private LayeAst.StructDeclaration? ReadStructDeclaration()
    {
        if (!ExpectKeyword(Keyword.Struct, out var structKw))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `struct` to begin struct declaration"));
            return null;
        }

        if (!ExpectIdentifier(out var structName))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected identifier for struct name"));
            return null;
        }

        if (!ExpectDelimiter(Delimiter.OpenBrace, out _))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `{` to open struct fields"));
            return null;
        }

        var fields = new List<LayeAst.ParamData>();
        while (!IsEoF && !CheckDelimiter(Delimiter.CloseBrace))
        {
            if (ReadContainerModifierList() is not { } modifiers)
            {
                AssertHasErrors("reading field modifier list");
                return null;
            }

            var fieldType = TryReadTypeNode(true);
            if (fieldType is null)
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected field type"));
                return null;
            }

            if (!ExpectIdentifier(out var fieldName))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected field name"));
                return null;
            }

            fields.Add(new(new(modifiers, fieldType, fieldName), null, null));

            if (!ExpectDelimiter(Delimiter.SemiColon, out var _))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `;` after field"));
                return null;
            }
        }

        if (!ExpectDelimiter(Delimiter.CloseBrace, out _))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `}` to close struct fields"));
            return null;
        }

        return new LayeAst.StructDeclaration(structKw, structName, fields.ToArray());
    }
    
    private LayeAst.FunctionDeclaration? ReadFunctionDeclaration()
    {
        if (ReadFunctionModifierList() is not LayeAst.FunctionModifiers functionModifiers)
        {
            AssertHasErrors("failing to read function decl modifiers");
            return null;
        }

        var returnType = TryReadTypeNode();
        if (returnType is null)
        {
            AssertHasErrors("failing to read a function return type");
            return null;
        }

        if (!ReadCallingConventionInto(functionModifiers))
        {
            AssertHasErrors("failing to read function decl calling convention");
            return null;
        }

        if (!ExpectIdentifier(out var functionName))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected identifier as function name"));
            return null;
        }

        return ReadFunctionDeclaration(functionModifiers, returnType, functionName);
    }

    private LayeAst.FunctionDeclaration? ReadFunctionDeclaration(LayeAst.FunctionModifiers modifiers, LayeAst.Type returnType, LayeToken.Identifier functionName)
    {
        if (!ExpectDelimiter(Delimiter.OpenParen, out var openParams))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `(` as start of function parameter list"));
            return null;
        }

        var paramData = new List<LayeAst.ParamData>();
        var paramDelims = new List<LayeToken.Delimiter>();

        VarArgsKind vaKind = VarArgsKind.None;
        LayeToken.Keyword? varargsKeyword = null;

        while (!CheckDelimiter(Delimiter.CloseParen, out var _))
        {
            if (CheckKeyword(Keyword.VarArgs, out varargsKeyword))
            {
                Advance(); // `varargs`

                if (CheckDelimiter(Delimiter.CloseParen, out var _))
                {
                    vaKind = VarArgsKind.C;
                    break;
                }
                else vaKind = VarArgsKind.Laye;
            }

            var paramType = TryReadTypeNode(true);
            if (paramType is null)
            {
                AssertHasErrors("failing to parse function parameter type");
                return null;
            }

            if (!ExpectIdentifier(out var paramName))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected identifier as parameter name"));
                return null;
            }

            var paramBinding = new LayeAst.BindingData(new(), paramType, paramName);
            paramData.Add(new LayeAst.ParamData(paramBinding, null, null));

            if (vaKind != VarArgsKind.None)
                break;

            if (CheckDelimiter(Delimiter.Comma, out var comma))
            {
                Advance(); // `,`
                paramDelims.Add(comma);
            }
            else break;
        }

        if (!ExpectDelimiter(Delimiter.CloseParen, out var closeParams))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `)` as end of function parameter list"));
            return null;
        }

        // TODO(local): other function body kinds
        LayeAst.FunctionBody body;
        if (CheckDelimiter(Delimiter.SemiColon, out var emptyBodySemi))
        {
            Advance(); // `;`
            body = new LayeAst.EmptyFunctionBody(emptyBodySemi);
        }
        else
        {
            var bodyBlock = ReadBlock();
            if (bodyBlock is null)
            {
                AssertHasErrors("failing to read function block body");
                return null;
            }

            body = new LayeAst.BlockFunctionBody(bodyBlock);
        }

        return new LayeAst.FunctionDeclaration(modifiers, returnType, functionName, openParams, paramData.ToArray(), paramDelims.ToArray(), varargsKeyword, vaKind, closeParams, body);
    }

    private LayeAst.Block? ReadBlock()
    {
        if (!ExpectDelimiter(Delimiter.OpenBrace, out var openBlock))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `{` as start of block"));
            return null;
        }

        var bodyStatements = new List<LayeAst.Stmt>();
        while (!CheckDelimiter(Delimiter.CloseBrace, out var _))
        {
            var statement = ReadStatement();
            if (statement is null)
            {
                AssertHasErrors("failing to read statement in block");
                return null;
            }

            bodyStatements.Add(statement);
        }

        if (!ExpectDelimiter(Delimiter.CloseBrace, out var closeBlock))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `}` as end of block"));
            return null;
        }

        return new LayeAst.Block(openBlock, closeBlock, bodyStatements.ToArray());
    }

    private LayeAst.Stmt? ReadStatement()
    {
        if (CheckKeyword(Keyword.Return, out var returnKw))
        {
            Advance(); // `return`

            LayeAst.Expr? returnValue = null;
            if (!CheckDelimiter(Delimiter.SemiColon))
            {
                returnValue = ReadExpression();
                if (returnValue is null)
                {
                    AssertHasErrors("reading return value");
                    return null;
                }

                if (!ExpectDelimiter(Delimiter.SemiColon, out _))
                {
                    m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `;` to close return"));
                    return null;
                }
            }
            else Advance(); // `;`

            return new LayeAst.Return(returnKw, returnValue);
        }

        int startPosition = m_tokenIndex;
        if (TryReadTypeNode(false) is { } bindingType)
        {
            var functionModifiers = new LayeAst.FunctionModifiers();
            if (!ReadCallingConventionInto(functionModifiers, false))
                goto expressionStatement;

            if (CheckIdentifier(out var bindingIdent))
            {
                Advance(); // identifier
                if (!functionModifiers.IsEmpty || CheckDelimiter(Delimiter.OpenParen, out var _))
                    return ReadFunctionDeclaration(functionModifiers, bindingType, bindingIdent);

                LayeToken.Operator? opEqToken = null;
                LayeAst.Expr? assignedValue = null;

                if (CheckOperator(Operator.Assign, out var opEq))
                {
                    opEqToken = opEq;
                    Advance(); // `=`

                    if (ReadExpression() is not LayeAst.Expr expr)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected expression in binding assignment"));
                        return null;
                    }

                    assignedValue = expr;
                }

                if (!ExpectDelimiter(Delimiter.SemiColon, out var bindingSemi))
                {
                    m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `;` to terminate binding declaration"));
                    return null;
                }

                return new LayeAst.BindingDeclaration(bindingType, bindingIdent, opEqToken, assignedValue, bindingSemi);
            }
        }

    expressionStatement:
        m_tokenIndex = startPosition;

        var expression = ReadExpression();
        if (expression is null)
        {
            AssertHasErrors("failing to read expression as statement");
            return null;
        }

        if (!ExpectDelimiter(Delimiter.SemiColon, out var semi))
        {
            m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `;` to terminate expression statement"));
            return null;
        }

        return new LayeAst.ExpressionStatement(expression, semi);
    }

    private LayeAst.Expr? ReadExpression()
    {
        return ReadPrimaryExpression();
    }

    private LayeAst.Expr? ReadPrimaryExpression()
    {
        LayeAst.Expr? result = null;

        if (CheckDelimiter(Delimiter.OpenParen, out var openGrouped))
        {
            Advance(); // `(`

            var groupedExpression = ReadExpression();
            if (groupedExpression is null)
            {
                AssertHasErrors("failing to parse expression in parenthetical grouping");
                return null;
            }

            if (!ExpectDelimiter(Delimiter.CloseParen, out var closeGrouped))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `)` to close grouped expression"));
                return null;
            }

            result = new LayeAst.GroupedExpression(openGrouped, groupedExpression, closeGrouped);
        }
        else if (CheckIdentifier(out var ident))
        {
            Advance(); // identifier
            result = new LayeAst.NameLookup(ident);
        }
        else if (Check<LayeToken.Integer>(out var integerLit))
        {
            Advance(); // integer
            result = new LayeAst.Integer(integerLit);
        }
        else if (Check<LayeToken.Float>(out var floatLit))
        {
            Advance(); // float
            result = new LayeAst.Float(floatLit);
        }
        else if (Check<LayeToken.String>(out var stringLit))
        {
            Advance(); // string
            result = new LayeAst.String(stringLit);
        }
        else if (CheckOperator(out var prefixOp, op => op.Kind != Operator.Assign))
        {
            Advance(); // operator

            var subexpr = ReadPrimaryExpression();
            if (subexpr is null)
            {
                AssertHasErrors("failing to parse primary expression after prefix operator");
                return null;
            }

            if (prefixOp.Kind == Operator.Subtract)
            {
                if (subexpr is LayeAst.Integer subint)
                    return new LayeAst.Integer(new LayeToken.Integer(SourceSpan.Combine(prefixOp.SourceSpan, subint.SourceSpan), (ulong)-(long)subint.Literal.LiteralValue), !subint.Signed);
                else if (subexpr is LayeAst.Float subfloat)
                    return new LayeAst.Float(new LayeToken.Float(SourceSpan.Combine(prefixOp.SourceSpan, subfloat.SourceSpan), -subfloat.Literal.LiteralValue));
            }
            else if (prefixOp.Kind == Operator.Add)
            {
                if (subexpr is LayeAst.Integer subint)
                    return new LayeAst.Integer(new LayeToken.Integer(SourceSpan.Combine(prefixOp.SourceSpan, subint.SourceSpan), subint.Literal.LiteralValue));
                else if (subexpr is LayeAst.Float subfloat)
                    return new LayeAst.Float(new LayeToken.Float(SourceSpan.Combine(prefixOp.SourceSpan, subfloat.SourceSpan), subfloat.Literal.LiteralValue));
            }
            else throw new NotImplementedException();
        }
        else m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "unexpected token when parsing primary expression"));

        if (result is null)
        {
            AssertHasErrors("failing to create primary expression node");
            return null;
        }

        return ReadPrimaryExpressionSuffix(result);
    }

    private LayeAst.Expr? ReadPrimaryExpressionSuffix(LayeAst.Expr primary)
    {
        if (CheckDelimiter(Delimiter.Dot))
        {
            Advance(); // `.`

            if (!ExpectIdentifier(out var indexName))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected identifier as field index"));
                return null;
            }

            return ReadPrimaryExpressionSuffix(new LayeAst.NamedIndex(primary, indexName));
        }
        else if (CheckDelimiter(Delimiter.OpenParen, out var openInvoke))
        {
            Advance(); // `(`

            var args = new List<LayeAst.Expr>();
            var argsDelims = new List<LayeToken.Delimiter>();
            
            while (!CheckDelimiter(Delimiter.CloseParen))
            {
                var argument = ReadExpression();
                if (argument is null)
                {
                    AssertHasErrors("failing to parse argument expression");
                    return null;
                }

                args.Add(argument);

                if (CheckDelimiter(Delimiter.Comma, out var comma))
                {
                    Advance();
                    argsDelims.Add(comma);

                    continue;
                }
                else break;
            }

            if (!ExpectDelimiter(Delimiter.CloseParen, out var closeInvoke))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `)` to close invocation parameter list"));
                return null;
            }

            var invokeExpression = new LayeAst.Invoke(primary, openInvoke, args.ToArray(), argsDelims.ToArray(), closeInvoke);
            return ReadPrimaryExpressionSuffix(invokeExpression);
        }
        // TODO(local): figure out how to add access information to slices etc (e.g. `buffer readonly[0:10]` ?)
        else if (CheckDelimiter(Delimiter.OpenBracket))
        {
            Advance(); // `[`

            if (CheckDelimiter(Delimiter.Colon))
            {
                Advance(); // `:`
                if (!ReadCommaSeparatedExpressions("dynamic index", out var postSliceArgs))
                {
                    AssertHasErrors("reading comma separated expressions");
                    return null;
                }

                if (postSliceArgs.Count != 1)
                {
                    m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(postSliceArgs.ToArray()), "exactly one expression expected for slice count argument"));
                    return null;
                }

                if (!ExpectDelimiter(Delimiter.CloseBracket, out var _))
                {
                    m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `]` to close slice argument list"));
                    return null;
                }

                return ReadPrimaryExpressionSuffix(new LayeAst.Slice(primary, null, postSliceArgs[0]));
            }

            if (!ReadCommaSeparatedExpressions("dynamic index", out var args))
            {
                AssertHasErrors("reading comma separated expressions");
                return null;
            }

            if (CheckDelimiter(Delimiter.Colon))
            {
                Advance(); // `:`

                if (args.Count != 1)
                {
                    m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(args.ToArray()), "exactly one expression expected for slice offset argument"));
                    return null;
                }

                if (CheckDelimiter(Delimiter.CloseBracket))
                {
                    Advance(); // `]`
                    return ReadPrimaryExpressionSuffix(new LayeAst.Slice(primary, args[0], null));
                }

                if (!ReadCommaSeparatedExpressions("dynamic index", out var postSliceArgs))
                {
                    AssertHasErrors("reading comma separated expressions");
                    return null;
                }

                if (postSliceArgs.Count != 1)
                {
                    m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(postSliceArgs.ToArray()), "exactly one expression expected for slice count argument"));
                    return null;
                }

                if (!ExpectDelimiter(Delimiter.CloseBracket, out var _))
                {
                    m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `]` to close slice argument list"));
                    return null;
                }

                return ReadPrimaryExpressionSuffix(new LayeAst.Slice(primary, args[0], postSliceArgs[0]));
            }

            if (!ExpectDelimiter(Delimiter.CloseBracket, out var _))
            {
                m_diagnostics.Add(new Diagnostic.Error(MostRecentTokenSpan, "expected `]` to close dynamic index argument list"));
                return null;
            }

            var dynamicIndexExpression = new LayeAst.DynamicIndex(primary, args.ToArray());
            return ReadPrimaryExpressionSuffix(dynamicIndexExpression);
        }

        bool ReadCommaSeparatedExpressions(string argsKind, out List<LayeAst.Expr> args)
        {
            args = new List<LayeAst.Expr>();
            while (!IsEoF && !CheckDelimiter(Delimiter.CloseParen) &&
                !CheckDelimiter(Delimiter.CloseBracket) && !CheckDelimiter(Delimiter.Colon))
            {
                var argument = ReadExpression();
                if (argument is null)
                {
                    AssertHasErrors($"failing to parse {argsKind} argument expression");
                    return false;
                }

                args.Add(argument);

                if (CheckDelimiter(Delimiter.Comma))
                {
                    Advance();
                    continue;
                }
                else break;
            }

            return true;
        }

        return primary;
    }
}
