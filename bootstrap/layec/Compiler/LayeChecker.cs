using System.Diagnostics;

namespace laye.Compiler;

internal sealed class LayeChecker
{
    public static LayeCstRoot[] CheckSyntax(LayeAstRoot[] syntax, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        var checker = new LayeChecker(syntax, symbols, diagnostics);
        return checker.CreateConcreteTrees();
    }

    private readonly LayeAstRoot[] m_syntax;
    private readonly SymbolTable m_globalSymbols;

    private readonly List<Diagnostic> m_diagnostics;
    private readonly int m_originalErrorCount;

    private readonly Stack<SymbolTable> m_scopes = new();
    private SymbolTable CurrentScope => m_scopes.TryPeek(out var scope) ? scope : m_globalSymbols;
    private void PushScope(SymbolTable scope) => m_scopes.Push(scope);
    private void PopScope() => m_scopes.Pop();

    private LayeChecker(LayeAstRoot[] syntax, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        m_syntax = syntax;
        m_globalSymbols = symbols;
        m_diagnostics = diagnostics;

        m_originalErrorCount = diagnostics.Count(d => d is Diagnostic.Error);
    }

    private void AssertHasErrors(string context)
    {
        int errorCount = m_diagnostics.Count(d => d is Diagnostic.Error) - m_originalErrorCount;
        Debug.Assert(errorCount > 0, $"No error diagnostics generated when {context}");
    }

    private LayeCstRoot[] CreateConcreteTrees()
    {
        var syms = new Dictionary<LayeAst, Symbol>();

        // create symbols
        foreach (var astRoot in m_syntax)
        {
            var topLevelNodes = astRoot.TopLevelNodes;
            foreach (var node in topLevelNodes)
            {
                switch (node)
                {
                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        var sym = new Symbol.Function(fnDecl.Name.Image);
                        syms[fnDecl] = sym;

                        if (!m_globalSymbols.AddSymbol(sym))
                        {
                            m_diagnostics.Add(new Diagnostic.Error(fnDecl.Name.SourceSpan, $"`{sym.Name}` is already define in this scope (function overloading is not supported)"));
                            return Array.Empty<LayeCstRoot>();
                        }
                    } break;
                }
            }
        }

        // populate symbol data
        foreach (var astRoot in m_syntax)
        {
            var topLevelNodes = astRoot.TopLevelNodes;
            foreach (var node in topLevelNodes)
            {
                switch (node)
                {
                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        var sym = (Symbol.Function)syms[fnDecl];

                        var returnType = ResolveType(fnDecl.ReturnType);
                        if (returnType is null)
                        {
                            AssertHasErrors("failing to resolve function return type in symbol creation");
                            return Array.Empty<LayeCstRoot>();
                        }

                        var paramInfos = new List<(SymbolType, string)>();
                        foreach (var paramAstType in fnDecl.Parameters)
                        {
                            var paramType = ResolveType(paramAstType.Binding.BindingType);
                            if (paramType is null)
                            {
                                AssertHasErrors("failing to resolve function parameter type in symbol creation");
                                return Array.Empty<LayeCstRoot>();
                            }

                            paramInfos.Add((paramType, paramAstType.Binding.BindingName.Image));
                        }

                        var ccKind = CallingConvention.LayeNoContext;
                        var vaKind = fnDecl.VarArgsKind;

                        sym.Type = new SymbolType.Function(fnDecl.Name.Image, Array.Empty<TypeParam>(), ccKind, returnType, paramInfos.ToArray(), vaKind);
                    } break;
                }
            }
        }

        var cstRoots = new List<LayeCstRoot>();

        // compile functions to ir
        foreach (var astRoot in m_syntax)
        {
            var topLevelNodes = new List<LayeCst>();
            foreach (var node in astRoot.TopLevelNodes)
            {
                switch (node)
                {
                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        var sym = (Symbol.Function)syms[fnDecl];

                        var fn = CheckFunction(fnDecl, sym);
                        if (fn is null)
                        {
                            AssertHasErrors("failing to compile function");
                            return Array.Empty<LayeCstRoot>();
                        }

                        topLevelNodes.Add(fn);
                    } break;
                }
            }

            cstRoots.Add(new LayeCstRoot(topLevelNodes.ToArray()));
        }

        return cstRoots.ToArray();
    }

    private SymbolType? ResolveType(LayeAst.Type astType)
    {
        switch (astType)
        {
            case LayeAst.BuiltInType builtInType:
            {
                var kw = builtInType.BuiltInKeyword;
                switch (kw.Kind)
                {
                    case Keyword.Void: return new SymbolType.Void();

                    case Keyword.Bool: return new SymbolType.Bool();
                    case Keyword.SizedBool: return new SymbolType.SizedBool(kw.SizeData);

                    case Keyword.Rune: return new SymbolType.Rune();

                    case Keyword.Int: return new SymbolType.Integer(true);
                    case Keyword.SizedInt: return new SymbolType.SizedInteger(true, kw.SizeData);

                    case Keyword.UInt: return new SymbolType.Integer(false);
                    case Keyword.SizedUInt: return new SymbolType.SizedInteger(false, kw.SizeData);

                    case Keyword.Float: return new SymbolType.Float();
                    case Keyword.SizedFloat: return new SymbolType.SizedFloat(kw.SizeData);

                    case Keyword.RawPtr: return new SymbolType.RawPtr();

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, "failed to resolve type (unrecognized built-in type)"));
                        return null;
                    }
                }
            }

            case LayeAst.PointerType pointerType:
            {
                var elementType = ResolveType(pointerType.ElementType);
                if (elementType is null)
                {
                    AssertHasErrors("failing to resolve pointer element type");
                    return null;
                }

                return new SymbolType.Pointer(elementType);
            }

            case LayeAst.BufferType bufferType:
            {
                var elementType = ResolveType(bufferType.ElementType);
                if (elementType is null)
                {
                    AssertHasErrors("failing to resolve buffer element type");
                    return null;
                }

                return new SymbolType.Buffer(elementType);
            }

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, "failed to resolve type (unrecognized type)"));
                return null;
            }
        }
    }

    private LayeCst.FunctionDeclaration? CheckFunction(LayeAst.FunctionDeclaration fnDecl, Symbol.Function sym)
    {
        var functionScope = new SymbolTable(CurrentScope) { FunctionSymbol = sym };
        PushScope(functionScope);

        LayeCst.FunctionBody functionBody;
        switch (fnDecl.Body)
        {
            case LayeAst.EmptyFunctionBody: functionBody = new LayeCst.EmptyFunctionBody(); break;

            case LayeAst.BlockFunctionBody blockFunctionBody:
            {
                var block = CheckBlock(blockFunctionBody.BodyBlock);
                if (block is null)
                {
                    AssertHasErrors("failing to parse function body block");
                    return null;
                }

                functionBody = new LayeCst.BlockFunctionBody(block);
            } break;

            default: throw new NotImplementedException();
        }

        PopScope();

        var modifiers = fnDecl.Modifiers.Select<LayeAst.Modifier, LayeCst.Modifier>(m => m switch
        {
            LayeAst.Visibility vis => new LayeCst.Visibility(vis.VisibilityKeyword),
            LayeAst.CallingConvention cc => new LayeCst.CallingConvention(cc.ConventionKeyword),
            LayeAst.FunctionHint hint => new LayeCst.FunctionHint(hint.HintKeyword),
            LayeAst.Accessibility acc => new LayeCst.Accessibility(acc.AccessKeyword),
            _ => throw new NotImplementedException(),
        });

        return new LayeCst.FunctionDeclaration(modifiers.ToArray(), fnDecl.Name, sym, functionBody);
    }

    private LayeCst.Stmt? CheckStatement(LayeAst.Stmt statement)
    {
        switch (statement)
        {
            case LayeAst.ExpressionStatement exprStmt:
                var expr = CheckExpression(exprStmt.Expression);
                if (expr is null)
                {
                    AssertHasErrors("failing to compile expression statement");
                    return null;
                }

                return new LayeCst.ExpressionStatement(expr);

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "unrecognized statement type"));
                return null;
            }
        }
    }

    private LayeCst.Block? CheckBlock(LayeAst.Block block)
    {
        var blockScope = new SymbolTable(CurrentScope);
        PushScope(blockScope);

        var bodyNodes = new List<LayeCst.Stmt>();
        foreach (var node in block.Body)
        {
            var cstNode = CheckStatement(node);
            if (cstNode is null)
            {
                AssertHasErrors("failing to check block node");
                return null;
            }

            bodyNodes.Add(cstNode);
        }

        PopScope();

        return new LayeCst.Block(block.SourceSpan, bodyNodes.ToArray());
    }

    private LayeCst.Expr? CheckExpression(LayeAst.Expr statement)
    {
        switch (statement)
        {
            //case LayeAst.NameLookup nameLookupExpr: { }

            case LayeAst.Integer intExpr: return new LayeCst.Integer(intExpr.Literal, new SymbolType.UntypedInteger());
            case LayeAst.Float floatExpr: return new LayeCst.Float(floatExpr.Literal, new SymbolType.UntypedFloat());
            case LayeAst.Bool boolExpr: return new LayeCst.Bool(boolExpr.Literal, new SymbolType.UntypedBool());
            case LayeAst.String stringExpr: return new LayeCst.String(stringExpr.Literal, new SymbolType.UntypedString());

            case LayeAst.Invoke invokeExpr: return CheckInvoke(invokeExpr);

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "unrecognized expression type"));
                return null;
            }
        }
    }

    private LayeCst.Expr? CheckInvoke(LayeAst.Invoke invoke)
    {
        var argValues = new List<LayeCst.Expr>();
        foreach (var arg in invoke.Arguments)
        {
            var argValue = CheckExpression(arg);
            if (argValue is null)
            {
                AssertHasErrors("failing to check invocation argument");
                return null;
            }

            argValues.Add(argValue);
        }

        if (invoke.TargetExpression is LayeAst.NameLookup targetNameLookup)
        {
            string targetName = targetNameLookup.Name.Image;
            if (CurrentScope.LookupSymbol(targetName) is not Symbol symbol)
            {
                m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"failed to find function `{targetName}`"));
                return null;
            }

            if (symbol is not Symbol.Function fnSymbol)
            {
                m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"failed to find function `{targetName}`"));
                return null;
            }

            Debug.Assert(fnSymbol.Type is not null, "function declaration was not yet type checked");
            var targetParams = fnSymbol.Type.Parameters;

            switch (fnSymbol.Type.VarArgs)
            {
                case VarArgsKind.None:
                {
                    if (targetParams.Length != argValues.Count)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected {targetParams.Length} arguments to function `{targetName}`, got {argValues.Count}"));
                        return null;
                    }
                } break;

                case VarArgsKind.C:
                {
                    if (targetParams.Length > argValues.Count)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected at least {targetParams.Length} arguments to C-style varargs function `{targetName}`, got {argValues.Count}"));
                        return null;
                    }
                } break;

                default: throw new NotImplementedException($"varargs kind not handled in checker");
            }

            for (int i = 0; i < argValues.Count; i++)
            {
                var arg = argValues[i];
                var param = targetParams[i];

                if (CheckImplicitTypeCast(arg, param.Type) is not LayeCst.Expr argCast)
                {
                    AssertHasErrors("failing to convert argument to the correct type");
                    return null;
                }

                argValues[i] = argCast;
            }

            return new LayeCst.InvokeFunction(invoke.SourceSpan, fnSymbol, argValues.ToArray());
        }
        else
        {
            m_diagnostics.Add(new Diagnostic.Error(invoke.TargetExpression.SourceSpan, "can only invoke top level functions"));
            return null;
        }
    }

    private LayeCst.Expr? CheckImplicitTypeCast(LayeCst.Expr value, SymbolType targetType)
    {
        if (value.Type == targetType)
            return value;

        switch (value.Type)
        {
            case SymbolType.UntypedInteger:
            {
                if (targetType is SymbolType.Integer _targetIntType)
                {
                    if (value is LayeCst.Integer _int)
                        return new LayeCst.Integer(_int.Literal, _targetIntType);
                }
                else if (targetType is SymbolType.SizedInteger _targetSizedIntType)
                {
                    if (value is LayeCst.Integer _int)
                        return new LayeCst.Integer(_int.Literal, _targetSizedIntType);
                }
                else if (targetType is SymbolType.RawPtr)
                {
                    if (value is LayeCst.Integer _int && _int.Type is SymbolType.UntypedInteger)
                        value = new LayeCst.Integer(_int.Literal, new SymbolType.Integer(false));

                    return new LayeCst.TypeCast(value.SourceSpan, value, new SymbolType.RawPtr());
                }
            } break;

            case SymbolType.UntypedString:
            {
                //new SymbolType.Buffer(new SymbolType.SizedInteger(false, 8))
                if (targetType is SymbolType.Buffer _targetBufferType && _targetBufferType.ElementType == new SymbolType.SizedInteger(false, 8))
                {
                    if (value is LayeCst.String _string)
                        return new LayeCst.String(_string.Literal, _targetBufferType);
                }
            } break;
        }

        m_diagnostics.Add(new Diagnostic.Error(value.SourceSpan, $"unable to convert from {value.Type} to {targetType}"));
        return null;
    }
}
