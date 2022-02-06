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
                    case LayeAst.StructDeclaration structDecl:
                    {
                        var sym = new Symbol.Struct(structDecl.Name.Image);
                        syms[structDecl] = sym;

                        if (!m_globalSymbols.AddSymbol(sym))
                        {
                            m_diagnostics.Add(new Diagnostic.Error(structDecl.Name.SourceSpan, $"`{sym.Name}` is already defined in this scope"));
                            return Array.Empty<LayeCstRoot>();
                        }
                    } break;

                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        var sym = new Symbol.Function(fnDecl.Name.Image);
                        syms[fnDecl] = sym;

                        if (!m_globalSymbols.AddSymbol(sym))
                        {
                            m_diagnostics.Add(new Diagnostic.Error(fnDecl.Name.SourceSpan, $"`{sym.Name}` is already defined in this scope (function overloading is not supported)"));
                            return Array.Empty<LayeCstRoot>();
                        }
                    } break;

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(node.SourceSpan, $"internal compiler error: unrecognized syntax at top level ({node.GetType().Name})"));
                        return Array.Empty<LayeCstRoot>();
                    }
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
                    case LayeAst.StructDeclaration structDecl:
                    {
                        var sym = (Symbol.Struct)syms[structDecl];

                        var fields = new List<(SymbolType, string)>();
                        foreach (var field in structDecl.Fields)
                        {
                            var fieldType = ResolveType(field.Binding.BindingType);
                            if (fieldType is null)
                            {
                                AssertHasErrors("resolving struct field type");
                                return Array.Empty<LayeCstRoot>();
                            }

                            fields.Add((fieldType, field.Binding.BindingName.Image));
                        }

                        sym.Type = new SymbolType.Struct(structDecl.Name.Image, fields.ToArray());
                    } break;

                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        var sym = (Symbol.Function)syms[fnDecl];

                        var returnType = ResolveType(fnDecl.ReturnType);
                        if (returnType is null)
                        {
                            AssertHasErrors("resolving function return type in symbol creation");
                            return Array.Empty<LayeCstRoot>();
                        }

                        var paramInfos = new List<(SymbolType, string)>();
                        foreach (var paramAstType in fnDecl.Parameters)
                        {
                            var paramType = ResolveType(paramAstType.Binding.BindingType);
                            if (paramType is null)
                            {
                                AssertHasErrors("resolving function parameter type in symbol creation");
                                return Array.Empty<LayeCstRoot>();
                            }

                            paramInfos.Add((paramType, paramAstType.Binding.BindingName.Image));
                        }

                        var ccKind = fnDecl.Modifiers.CallingConvention;
                        var vaKind = fnDecl.VarArgsKind;

                        sym.Type = new SymbolType.Function(fnDecl.Name.Image, ccKind, returnType, paramInfos.ToArray(), vaKind);
                    } break;

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(node.SourceSpan, $"internal compiler error: unrecognized syntax at top level ({node.GetType().Name})"));
                        return Array.Empty<LayeCstRoot>();
                    }
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
                    case LayeAst.StructDeclaration: break;

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

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(node.SourceSpan, $"internal compiler error: unrecognized syntax at top level ({node.GetType().Name})"));
                        return Array.Empty<LayeCstRoot>();
                    }
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
            case LayeAst.NamedType namedType:
            {
                var path = namedType.TypePath;
                switch (path)
                {
                    case LayeAst.NamePathPart namePart:
                    {
                        var symbol = CurrentScope.LookupSymbol(namePart.Name.Image);
                        if (symbol is null)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"the name `{namePart.Name.Image}` does not exist in the current context"));
                            return null;
                        }

                        switch (symbol)
                        {
                            case Symbol.Struct structSymbol: return structSymbol.Type!;

                            default:
                            {
                                m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"`{namePart.Name.Image}` is not a type"));
                                return null;
                            }
                        }
                    }

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, "failed to resolve type (unable to resolve path)"));
                        return null;
                    }
                }
            }

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
                    case Keyword.String: return new SymbolType.String();

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
                    AssertHasErrors("resolving pointer element type");
                    return null;
                }

                return new SymbolType.Pointer(elementType, pointerType.Modifiers.Access);
            }

            case LayeAst.BufferType bufferType:
            {
                var elementType = ResolveType(bufferType.ElementType);
                if (elementType is null)
                {
                    AssertHasErrors("resolving buffer element type");
                    return null;
                }

                return new SymbolType.Buffer(elementType, bufferType.Modifiers.Access);
            }

            case LayeAst.FunctionType functionType:
            {
                var returnType = ResolveType(functionType.ReturnType);
                if (returnType is null)
                {
                    AssertHasErrors("resolving function return type");
                    return null;
                }

                var parameterTypes = new SymbolType[functionType.ParameterTypes.Length];
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    var paramType = ResolveType(functionType.ParameterTypes[i]);
                    if (paramType is null)
                    {
                        AssertHasErrors("resolving function parameter type");
                        return null;
                    }

                    parameterTypes[i] = paramType;
                }

                return new SymbolType.FunctionPointer(functionType.Modifiers.CallingConvention, returnType, parameterTypes, functionType.VarArgsKind);
            }

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"failed to resolve type (unrecognized type {astType.GetType().Name})"));
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

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(fnDecl.Body.SourceSpan, $"internal compiler error: unrecognized function body type {fnDecl.Body.GetType().Name}"));
                return null;
            }
        }

        PopScope();

        var modifiers = new LayeCst.FunctionModifiers()
        {
            ExternLibrary = fnDecl.Modifiers.ExternLibrary,
            Visibility = fnDecl.Modifiers.Visibility,
            CallingConvention = fnDecl.Modifiers.CallingConvention,
            FunctionHint = fnDecl.Modifiers.FunctionHint,
        };

        return new LayeCst.FunctionDeclaration(modifiers, fnDecl.Name, sym, functionBody);
    }

    private LayeCst.Stmt? CheckStatement(LayeAst.Stmt statement)
    {
        switch (statement)
        {
            case LayeAst.FunctionDeclaration functionDecl:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "functions not allowed outside of global scope yet"));
                return null;
            }

            case LayeAst.BindingDeclaration bindingDecl:
            {
                SymbolType? bindingType = null;

                if (bindingDecl.BindingType is not null)
                {
                    bindingType = ResolveType(bindingDecl.BindingType);
                    if (bindingType is null)
                    {
                        AssertHasErrors("failing to resolve binding type");
                        return null;
                    }
                }
                else if (bindingDecl.Expression is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "`var` binding requires an initialization expression to type check"));
                    return null;
                }

                LayeCst.Expr? expression = null;

                if (bindingDecl.Expression is not null)
                {
                    expression = CheckExpression(bindingDecl.Expression);
                    if (expression is null)
                    {
                        AssertHasErrors("failing to check binding expression");
                        return null;
                    }

                    if (bindingType is not null)
                    {
                        expression = CheckImplicitTypeCast(expression, bindingType);
                        if (expression is null)
                        {
                            AssertHasErrors("failing to check binding expression implicit cast to binding type");
                            return null;
                        }
                    }
                    else bindingType = expression.Type;
                }

                var bindingSymbol = new Symbol.Binding(bindingDecl.Name.Image, bindingType);
                CurrentScope.AddSymbol(bindingSymbol);

                return new LayeCst.BindingDeclaration(bindingDecl.Name, bindingSymbol, expression);
            }

            case LayeAst.Return returnStmt:
            {
                LayeCst.Expr? returnExpr = null;
                if (returnStmt.ReturnValue is { } expr)
                {
                    returnExpr = CheckExpression(expr);
                    if (returnExpr is null)
                    {
                        AssertHasErrors("checking return value");
                        return null;
                    }
                }

                var function = CurrentScope.FunctionSymbol;
                
                Debug.Assert(function is not null);
                Debug.Assert(function.Type is not null);

                if (returnExpr is not null)
                {
                    if (function.Type!.ReturnType is SymbolType.Void)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "cannot return a value as the function returns void"));
                        return null;
                    }

                    returnExpr = CheckImplicitTypeCast(returnExpr, function.Type.ReturnType);
                    if (returnExpr is null)
                    {
                        AssertHasErrors("checking return type implicit cast");
                        return null;
                    }
                }
                else
                {
                    if (function.Type!.ReturnType is not SymbolType.Void)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "must return a value as the function does not return void"));
                        return null;
                    }
                }

                return new LayeCst.Return(returnStmt.SourceSpan, returnExpr);
            }

            case LayeAst.ExpressionStatement exprStmt:
            {
                var expr = CheckExpression(exprStmt.Expression);
                if (expr is null)
                {
                    AssertHasErrors("failing to compile expression statement");
                    return null;
                }

                return new LayeCst.ExpressionStatement(expr);
            }

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

        bool isInDeadCodeMode = false;
        var deadNodes = new List<LayeCst.Stmt>();

        foreach (var node in block.Body)
        {
            var cstNode = CheckStatement(node);
            if (cstNode is null)
            {
                AssertHasErrors("failing to check block node");
                return null;
            }

            if (isInDeadCodeMode)
                deadNodes.Add(cstNode);
            else
            {
                bodyNodes.Add(cstNode);
                isInDeadCodeMode = cstNode.CheckReturns();
            }
        }

        PopScope();

        if (isInDeadCodeMode && deadNodes.Count > 0)
        {
            var deadNodesArr = deadNodes.ToArray();
            var deadNode = new LayeCst.DeadCode(SourceSpan.Combine(deadNodesArr), deadNodesArr);
            bodyNodes.Add(deadNode);

            m_diagnostics.Add(new Diagnostic.Warning(deadNode.SourceSpan, "unreachable code"));
        }

        return new LayeCst.Block(block.SourceSpan, bodyNodes.ToArray());
    }

    private LayeCst.Expr? CheckExpression(LayeAst.Expr statement)
    {
        switch (statement)
        {
            //case LayeAst.NameLookup nameLookupExpr: { }

            case LayeAst.Integer intLit: return new LayeCst.Integer(intLit.Literal, new SymbolType.UntypedInteger(intLit.Signed));
            case LayeAst.Float floatLit: return new LayeCst.Float(floatLit.Literal, new SymbolType.UntypedFloat());
            case LayeAst.Bool boolLit: return new LayeCst.Bool(boolLit.Literal, new SymbolType.UntypedBool());
            case LayeAst.String stringLit: return new LayeCst.String(stringLit.Literal, new SymbolType.UntypedString());

            case LayeAst.NameLookup nameLookupExpr:
            {
                var symbol = CurrentScope.LookupSymbol(nameLookupExpr.Name.Image);
                if (symbol is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(nameLookupExpr.Name.SourceSpan, $"the name `{nameLookupExpr.Name.Image}` does not exist in the current context"));
                    return null;
                }

                return new LayeCst.LoadValue(nameLookupExpr.SourceSpan, symbol);
            }

            case LayeAst.NamedIndex namedIndexExpr:
            {
                var target = CheckExpression(namedIndexExpr.TargetExpression);
                if (target is null)
                {
                    AssertHasErrors("checking named index target");
                    return null;
                }

                switch (target.Type)
                {
                    case SymbolType.String:
                    {
                        if (namedIndexExpr.Name.Image == "length")
                            return new LayeCst.StringLengthLookup(namedIndexExpr.SourceSpan, target);

                        m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, $"type `string` does not contain a field named `{namedIndexExpr.Name.Image}`"));
                        return null;
                    }

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, $"cannot index type {target.Type}"));
                        return null;
                    }
                }
            }

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
        var argValues = new LayeCst.Expr[invoke.Arguments.Length];
        for (int i = 0; i < argValues.Length; i++)
        {
            var arg = invoke.Arguments[i];

            var argValue = CheckExpression(arg);
            if (argValue is null)
            {
                AssertHasErrors("failing to check invocation argument");
                return null;
            }

            argValues[i] = argValue;
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
                    if (targetParams.Length != argValues.Length)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected {targetParams.Length} arguments to function `{targetName}`, got {argValues.Length}"));
                        return null;
                    }
                } break;

                case VarArgsKind.C:
                {
                    if (targetParams.Length > argValues.Length)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected at least {targetParams.Length} arguments to C-style varargs function `{targetName}`, got {argValues.Length}"));
                        return null;
                    }
                } break;

                default:
                {
                    m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"internal compiler error: varargs kind {fnSymbol.Type.VarArgs} not handled in checker"));
                    return null;
                }
            }

            int argsToCheckCount = Math.Min(argValues.Length, targetParams.Length);
            for (int i = 0; i < argsToCheckCount; i++)
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

            if (fnSymbol.Type.VarArgs == VarArgsKind.C)
            {
                for (int i = argsToCheckCount; i < argValues.Length; i++)
                {
                    var arg = argValues[i];

                    if (arg.Type is SymbolType.UntypedInteger unint)
                    {
                        var _int = arg as LayeCst.Integer;
                        Debug.Assert(_int is not null, "untyped integer found in non literal expression");

                        argValues[i] = new LayeCst.Integer(_int.Literal, new SymbolType.Integer(unint.Signed));
                    }
                    else if (arg.Type is SymbolType.SizedInteger sint && sint.BitCount < 32)
                        argValues[i] = new LayeCst.TypeCast(arg.SourceSpan, arg, new SymbolType.SizedInteger(sint.Signed, 32));
                    else if (arg.Type is SymbolType.SizedFloat sfloat && sfloat.BitCount < 64)
                        argValues[i] = new LayeCst.TypeCast(arg.SourceSpan, arg, new SymbolType.SizedFloat(64));
                }
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
                if (targetType is SymbolType.Buffer _targetBufferType && _targetBufferType.ElementType == new SymbolType.SizedInteger(false, 8) && _targetBufferType.Access == AccessKind.ReadOnly)
                {
                    if (value is LayeCst.String _string)
                        return new LayeCst.String(_string.Literal, _targetBufferType);
                }
                else if (targetType is SymbolType.String _targetStringType)
                {
                    if (value is LayeCst.String _string)
                        return new LayeCst.String(_string.Literal, _targetStringType);
                }
            } break;

            case SymbolType.Pointer pointerType:
            {
                if (targetType is SymbolType.Pointer _targetPointerType && _targetPointerType.ElementType == pointerType.ElementType)
                {
                    if (_targetPointerType.Access == AccessKind.ReadOnly)
                        return new LayeCst.TypeCast(value.SourceSpan, value, pointerType);
                }
            } break;

            case SymbolType.Buffer bufferType:
            {
                if (targetType is SymbolType.Buffer _targetBufferType && _targetBufferType.ElementType == bufferType.ElementType)
                {
                    if (_targetBufferType.Access == AccessKind.ReadOnly)
                        return new LayeCst.TypeCast(value.SourceSpan, value, bufferType);
                }
            } break;
        }

        m_diagnostics.Add(new Diagnostic.Error(value.SourceSpan, $"unable to convert from {value.Type} to {targetType}"));
        return null;
    }
}
