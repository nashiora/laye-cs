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

    private readonly Dictionary<string, string> m_headerToImpls = new();
    private readonly Dictionary<string, string> m_implToHeader = new();

    private readonly Stack<SymbolType> m_typeContextStack = new();
    private SymbolType? CurrentTypeContext => m_typeContextStack.TryPeek(out var type) ? type : null;
    private void PushTypeContext(SymbolType type) => m_typeContextStack.Push(type);
    private void PopTypeContext() => m_typeContextStack.Pop();

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
                    
                    case LayeAst.EnumDeclaration enumDecl:
                    {
                        Debug.Assert(!enumDecl.HasBothEnumAndUnionVariants, "the parser didn't check for enum/union distinction");

                        Symbol sym;
                        if (enumDecl.IsUnion)
                            sym = new Symbol.Union(enumDecl.Name.Image);
                        else sym = new Symbol.Enum(enumDecl.Name.Image);

                        syms[enumDecl] = sym;

                        if (!m_globalSymbols.AddSymbol(sym))
                        {
                            m_diagnostics.Add(new Diagnostic.Error(enumDecl.Name.SourceSpan, $"`{sym.Name}` is already defined in this scope"));
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

        // create type symbols first, repeating until finished bc no dependency tree
        {
            bool isTypeCreationComplete = true;

            uint attemptCount = 0;
            uint MaxAttemptCount = 1_000;

            while (attemptCount < MaxAttemptCount)
            {
                isTypeCreationComplete = true;
                attemptCount++;

                bool isRequired = attemptCount == MaxAttemptCount - 1;

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
                                if (sym.Type is not null) continue;

                                bool structBuildFailed = false;

                                var fields = new (SymbolType, string)[structDecl.Fields.Length];
                                for (int i = 0; i < fields.Length; i++)
                                {
                                    var field = structDecl.Fields[i];

                                    var fieldType = ResolveType(field.Binding.BindingType, isRequired);
                                    if (fieldType is null)
                                    {
                                        if (isRequired)
                                        {
                                            AssertHasErrors("resolving struct field type");
                                            return Array.Empty<LayeCstRoot>();
                                        }
                                        else
                                        {
                                            structBuildFailed = true;
                                            break;
                                        }
                                    }

                                    fields[i] = (fieldType, field.Binding.BindingName.Image);
                                }

                                if (structBuildFailed)
                                {
                                    isTypeCreationComplete = false;
                                    break;
                                }

                                sym.Type = new SymbolType.Struct(structDecl.Name.Image, fields.ToArray());
                            } break;

                            case LayeAst.EnumDeclaration enumDecl:
                            {
                                var sym = syms[enumDecl];
                                if (sym.Type is not null) continue;

                                if (sym is Symbol.Union symUnion)
                                {
                                    bool unionBuildFailed = false;

                                    var variants = new SymbolType.UnionVariant[enumDecl.Variants.Length];
                                    for (int v = 0; !unionBuildFailed && v < enumDecl.Variants.Length; v++)
                                    {
                                        var variant = enumDecl.Variants[v];
                                        for (int j = 0; j < v; j++)
                                        {
                                            if (variants[j].Name == variant.VariantName.Image)
                                            {
                                                m_diagnostics.Add(new Diagnostic.Error(enumDecl.Name.SourceSpan, $"enum `{sym.Name}` already defines a variant `{variant.VariantName.Image}`"));
                                                return Array.Empty<LayeCstRoot>();
                                            }
                                        }

                                        var fields = new (SymbolType Type, string Name)[variant.VariantFields.Length];
                                        for (int i = 0; i < fields.Length; i++)
                                        {
                                            var field = variant.VariantFields[i];
                                            for (int j = 0; j < i; j++)
                                            {
                                                if (fields[j].Name == field.Binding.BindingName.Image)
                                                {
                                                    m_diagnostics.Add(new Diagnostic.Error(enumDecl.Name.SourceSpan, $"variant `{variant.VariantName.Image}` in enum `{sym.Name}` already defines a field `{field.Binding.BindingName.Image}`"));
                                                    return Array.Empty<LayeCstRoot>();
                                                }
                                            }

                                            var fieldType = ResolveType(field.Binding.BindingType, isRequired);
                                            if (fieldType is null)
                                            {
                                                if (isRequired)
                                                {
                                                    AssertHasErrors("resolving enum variant field type");
                                                    return Array.Empty<LayeCstRoot>();
                                                }
                                                else
                                                {
                                                    unionBuildFailed = true;
                                                    break;
                                                }
                                            }

                                            fields[i] = (fieldType, field.Binding.BindingName.Image);
                                        }

                                        if (!unionBuildFailed)
                                            variants[v] = new SymbolType.UnionVariant(variant.VariantName.Image, fields);
                                    }

                                    if (unionBuildFailed)
                                    {
                                        isTypeCreationComplete = false;
                                        break;
                                    }

                                    sym.Type = new SymbolType.Union((Symbol.Union)sym, enumDecl.Name.Image, variants);
                                }
                                else if (sym is Symbol.Enum symEnum)
                                {
                                    var variants = new (string Name, ulong Value)[enumDecl.Variants.Length];

                                    ulong variantValue = 0;
                                    for (int v = 0; v < enumDecl.Variants.Length; v++, variantValue++)
                                    {
                                        var variant = enumDecl.Variants[v];
                                        for (int j = 0; j < v; j++)
                                        {
                                            if (variants[j].Name == variant.VariantName.Image)
                                            {
                                                m_diagnostics.Add(new Diagnostic.Error(enumDecl.Name.SourceSpan, $"enum `{sym.Name}` already defines a variant `{variant.VariantName.Image}`"));
                                                return Array.Empty<LayeCstRoot>();
                                            }
                                        }

                                        if (variant.VariantValue is not null)
                                        {
                                            if (variant.VariantValue is LayeAst.Integer variantValueInteger)
                                                variantValue = variantValueInteger.Literal.LiteralValue;
                                            else
                                            {
                                                m_diagnostics.Add(new Diagnostic.Error(variant.SourceSpan, "only constant unsigned integer literal values are supported as enum values"));
                                                return Array.Empty<LayeCstRoot>();
                                            }
                                        }

                                        variants[v] = (variant.VariantName.Image, variantValue);
                                    }

                                    sym.Type = new SymbolType.Enum((Symbol.Enum)sym, enumDecl.Name.Image, variants);
                                }
                                else throw new NotImplementedException("unreachable");
                            } break;
                        }
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
                    case LayeAst.StructDeclaration structDecl: break;
                    case LayeAst.EnumDeclaration structDecl: break;

                    case LayeAst.FunctionDeclaration fnDecl:
                    {
                        Symbol.Function sym = (Symbol.Function)syms[fnDecl];

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
                    case LayeAst.EnumDeclaration: break;

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

    private SymbolType? ResolveType(LayeAst.Type astType, bool reportErrors = true)
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
                        Debug.Assert(namePart.Name is LayeToken.Identifier, "named typed was not an identifier");
                        string name = ((LayeToken.Identifier)namePart.Name).Image;

                        var symbol = CurrentScope.LookupSymbol(name);
                        if (symbol is null)
                        {
                            if (reportErrors)
                                m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"the name `{name}` does not exist in the current context"));
                            return null;
                        }

                        switch (symbol)
                        {
                            case Symbol.Struct structSymbol: return structSymbol.Type!;
                            case Symbol.Enum enumSymbol: return enumSymbol.Type!;
                            case Symbol.Union unionSymbol: return unionSymbol.Type!;

                            default:
                            {
                                if (reportErrors)
                                    m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"`{name}` is not a type"));
                                return null;
                            }
                        }
                    }

                    default:
                    {
                        if (reportErrors)
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
                    case Keyword.Void: return SymbolTypes.Void;

                    case Keyword.Bool: return SymbolTypes.Bool;
                    //case Keyword.SizedBool: return new SymbolType.SizedBool(kw.SizeData);

                    case Keyword.Rune: return SymbolTypes.Rune;

                    case Keyword.Int: return new SymbolType.Integer(true);
                    case Keyword.SizedInt: return new SymbolType.SizedInteger(true, kw.SizeData);

                    case Keyword.UInt: return new SymbolType.Integer(false);
                    case Keyword.SizedUInt: return new SymbolType.SizedInteger(false, kw.SizeData);

                    case Keyword.Float: return new SymbolType.Float();
#if false
                    case Keyword.SizedFloat: return new SymbolType.SizedFloat(kw.SizeData);
#endif

                    case Keyword.RawPtr: return SymbolTypes.RawPtr;
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
                    if (reportErrors)
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
                    if (reportErrors)
                        AssertHasErrors("resolving buffer element type");
                    return null;
                }

                return new SymbolType.Buffer(elementType, bufferType.Modifiers.Access);
            }

            case LayeAst.SliceType sliceType:
            {
                var elementType = ResolveType(sliceType.ElementType);
                if (elementType is null)
                {
                    if (reportErrors)
                        AssertHasErrors("resolving slice element type");
                    return null;
                }

                return new SymbolType.Slice(elementType, sliceType.Modifiers.Access);
            }

            case LayeAst.DynamicType dynType:
            {
                var elementType = ResolveType(dynType.ElementType);
                if (elementType is null)
                {
                    if (reportErrors)
                        AssertHasErrors("resolving dynamic element type");
                    return null;
                }

                return new SymbolType.Dynamic(elementType, dynType.Modifiers.Access);
            }

            case LayeAst.FunctionType functionType:
            {
                var returnType = ResolveType(functionType.ReturnType);
                if (returnType is null)
                {
                    if (reportErrors)
                        AssertHasErrors("resolving function return type");
                    return null;
                }

                var parameterTypes = new SymbolType[functionType.ParameterTypes.Length];
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    var paramType = ResolveType(functionType.ParameterTypes[i]);
                    if (paramType is null)
                    {
                        if (reportErrors)
                            AssertHasErrors("resolving function parameter type");
                        return null;
                    }

                    parameterTypes[i] = paramType;
                }

                return new SymbolType.FunctionPointer(functionType.Modifiers.CallingConvention, returnType, parameterTypes, functionType.VarArgsKind);
            }

            default:
            {
                if (reportErrors)
                    m_diagnostics.Add(new Diagnostic.Error(astType.SourceSpan, $"failed to resolve type (unrecognized type {astType.GetType().Name})"));
                return null;
            }
        }
    }

    private LayeCst.FunctionDeclaration? CheckFunction(LayeAst.FunctionDeclaration fnDecl, Symbol.Function sym)
    {
        var functionScope = new SymbolTable(CurrentScope) { FunctionSymbol = sym };
        PushScope(functionScope);

        var paramSymbols = new Symbol.Binding[fnDecl.Parameters.Length];
        for (int i = 0; i < fnDecl.Parameters.Length; i++)
        {
            var param = fnDecl.Parameters[i];
            var paramType = sym.Type!.Parameters[i].Type;

            var paramSymbol = new Symbol.Binding(param.Binding.BindingName.Image, paramType);
            paramSymbols[i] = paramSymbol;

            functionScope.AddSymbol(paramSymbol);
        }

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

                if (sym.Type!.ReturnType != SymbolTypes.Void && !block.CheckReturns())
                {
                    m_diagnostics.Add(new Diagnostic.Error(fnDecl.Body.SourceSpan, $"not all code paths return a value in function {sym.Name}"));
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
            CallingConvention = fnDecl.Modifiers.CallingConvention,
            FunctionHint = fnDecl.Modifiers.FunctionHint,
        };

        return new LayeCst.FunctionDeclaration(modifiers, fnDecl.Name, sym, paramSymbols, functionBody);
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

                bool pushed = bindingType is not null;
                if (pushed)
                    PushTypeContext(bindingType!);

                LayeCst.Expr? expression = null;

                if (bindingDecl.Expression is not null)
                {
                    expression = CheckExpression(bindingDecl.Expression);
                    if (expression is null)
                    {
                        AssertHasErrors("failing to check binding expression");
                        if (pushed)
                            PopTypeContext();
                        return null;
                    }

                    if (bindingType is not null)
                    {
                        expression = CheckImplicitTypeCast(expression, bindingType);
                        if (expression is null)
                        {
                            AssertHasErrors("failing to check binding expression implicit cast to binding type");
                            if (pushed)
                                PopTypeContext();
                            return null;
                        }
                    }
                    else bindingType = expression.Type;
                }

                if (pushed)
                    PopTypeContext();

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

            case LayeAst.Break breakStmt:
            {
                // TODO(local): check break/continue targets
                return new LayeCst.Break(breakStmt.SourceSpan);
            }

            case LayeAst.ExpressionStatement exprStmt:
            {
                var expr = CheckExpression(exprStmt.Expression);
                if (expr is null)
                {
                    AssertHasErrors("checking expression statement");
                    return null;
                }

                return new LayeCst.ExpressionStatement(expr);
            }

            case LayeAst.Assignment assignmentStmt:
            {
                var targetExpression = CheckExpression(assignmentStmt.TargetExpression);
                if (targetExpression is null)
                {
                    AssertHasErrors("checking assignment target");
                    return null;
                }

                if (!targetExpression.CheckIsLValue())
                {
                    m_diagnostics.Add(new Diagnostic.Error(assignmentStmt.TargetExpression.SourceSpan, "target of assignment must be an l-value"));
                    return null;
                }

                PushTypeContext(targetExpression.Type);

                var valueExpression = CheckExpression(assignmentStmt.ValueExpression);
                if (valueExpression is null)
                {
                    AssertHasErrors("checking assignment value");
                    PopTypeContext();
                    return null;
                }

                valueExpression = CheckImplicitTypeCast(valueExpression, targetExpression.Type);
                if (valueExpression is null)
                {
                    AssertHasErrors("checking assignment value implicit conversion");
                    PopTypeContext();
                    return null;
                }

                PopTypeContext();

                return new LayeCst.Assignment(targetExpression, valueExpression);
            }

            case LayeAst.DynamicAppend dynamicAppendStmt:
            {
                var targetExpression = CheckExpression(dynamicAppendStmt.TargetExpression);
                if (targetExpression is null)
                {
                    AssertHasErrors("checking dynamic append target");
                    return null;
                }

                if (!targetExpression.CheckIsLValue())
                {
                    m_diagnostics.Add(new Diagnostic.Error(dynamicAppendStmt.TargetExpression.SourceSpan, "target of dynamic append must be an l-value"));
                    return null;
                }

                if (targetExpression.Type is not SymbolType.Dynamic dynamicType)
                {
                    m_diagnostics.Add(new Diagnostic.Error(dynamicAppendStmt.TargetExpression.SourceSpan, "target of dynamic append must be a dynamic"));
                    return null;
                }

                PushTypeContext(dynamicType.ElementType);

                var valueExpression = CheckExpression(dynamicAppendStmt.ValueExpression);
                if (valueExpression is null)
                {
                    AssertHasErrors("checking dynamic append value");
                    PopTypeContext();
                    return null;
                }

                valueExpression = CheckImplicitTypeCast(valueExpression, dynamicType.ElementType);
                if (valueExpression is null)
                {
                    AssertHasErrors("checking dynamic append value implicit conversion");
                    PopTypeContext();
                    return null;
                }

                PopTypeContext();

                return new LayeCst.DynamicAppend(dynamicAppendStmt.SourceSpan, targetExpression, valueExpression);
            }

            case LayeAst.Block blockStmt:
            {
                PushScope(new SymbolTable(CurrentScope));

                var body = new LayeCst.Stmt[blockStmt.Body.Length];
                for (int i = 0; i < body.Length; i++)
                {
                    var stmt = CheckStatement(blockStmt.Body[i]);
                    if (stmt is null)
                    {
                        AssertHasErrors("checking statement in block");
                        return null;
                    }

                    body[i] = stmt;
                }

                PopScope();

                return new LayeCst.Block(blockStmt.SourceSpan, body);
            }

            case LayeAst.If ifStmt:
            {
                var condition = CheckExpression(ifStmt.Condition);
                if (condition is null)
                {
                    AssertHasErrors("checking if condition");
                    return null;
                }

                condition = CheckImplicitTypeCast(condition, SymbolTypes.Bool);
                if (condition is null)
                {
                    AssertHasErrors("checking if condition implicit conversion to bool");
                    return null;
                }

                var passBody = CheckStatement(ifStmt.IfBody);
                if (passBody is null)
                {
                    AssertHasErrors("checking if pass body");
                    return null;
                }

                LayeCst.Stmt? failBody = null;
                if (ifStmt.ElseBody is not null)
                {
                    failBody = CheckStatement(ifStmt.ElseBody);
                    if (failBody is null)
                    {
                        AssertHasErrors("checking if fail body");
                        return null;
                    }
                }

                return new LayeCst.If(condition, passBody, failBody);
            }

            case LayeAst.While whileStmt:
            {
                var condition = CheckExpression(whileStmt.Condition);
                if (condition is null)
                {
                    AssertHasErrors("checking while condition");
                    return null;
                }

                condition = CheckImplicitTypeCast(condition, SymbolTypes.Bool);
                if (condition is null)
                {
                    AssertHasErrors("checking while condition implicit conversion to bool");
                    return null;
                }

                var passBody = CheckStatement(whileStmt.WhileBody);
                if (passBody is null)
                {
                    AssertHasErrors("checking while pass body");
                    return null;
                }

                LayeCst.Stmt? failBody = null;
                if (whileStmt.ElseBody is not null)
                {
                    failBody = CheckStatement(whileStmt.ElseBody);
                    if (failBody is null)
                    {
                        AssertHasErrors("checking while fail body");
                        return null;
                    }
                }

                return new LayeCst.While(condition, passBody, failBody);
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

    private LayeCst.Expr? CheckExpression(LayeAst.Expr expression)
    {
        switch (expression)
        {
            //case LayeAst.NameLookup nameLookupExpr: { }

            case LayeAst.Integer intLit: return new LayeCst.Integer(intLit.Literal, intLit.Signed ? SymbolTypes.UntypedInt : SymbolTypes.UntypedUInt);
            case LayeAst.Float floatLit: return new LayeCst.Float(floatLit.Literal, new SymbolType.UntypedFloat());
            case LayeAst.Bool boolLit: return new LayeCst.Bool(boolLit.Literal, SymbolTypes.Bool);
            case LayeAst.String stringLit: return new LayeCst.String(stringLit.Literal, new SymbolType.UntypedString());
            case LayeAst.NullPtr nullptrLit: return new LayeCst.NullPtr(nullptrLit.Literal, SymbolTypes.RawPtr, SymbolTypes.U8);
            case LayeAst.Nil nilLit: return new LayeCst.Nil(nilLit.Literal, SymbolTypes.UntypedNil);

            case LayeAst.NameLookup nameLookupExpr:
            {
                var symbol = CurrentScope.LookupSymbol(nameLookupExpr.Name.Image);
                if (symbol is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(nameLookupExpr.Name.SourceSpan, $"the name `{nameLookupExpr.Name.Image}` does not exist in the current context"));
                    return null;
                }

                if (symbol is not Symbol.Binding)
                {
                    m_diagnostics.Add(new Diagnostic.Error(nameLookupExpr.Name.SourceSpan, $"the name `{nameLookupExpr.Name.Image}` does not refer to a variable"));
                    return null;
                }

                return new LayeCst.LoadValue(nameLookupExpr.SourceSpan, symbol);
            }

            case LayeAst.PathLookup pathLookupExpr:
            {
                // if we get here, we assume whatever we can load does not require construction arguments
                // if they do, the invoke expression will have checked for that

                switch (pathLookupExpr.Path)
                {
                    case LayeAst.JoinedPath joined:
                    {
                        Symbol baseSymbol;

                        // TODO(local): check if we've pushed a type onto the type context stack that we can use to infer an empty left-hand side

                        if (joined.BasePath is LayeAst.EmptyPathPart baseEmpty)
                        {
                            if (CurrentTypeContext is SymbolType.Enum typeContextEnum)
                                baseSymbol = typeContextEnum.EnumSymbol;
                            else if (CurrentTypeContext is SymbolType.Union typeContextUnion)
                                baseSymbol = typeContextUnion.UnionSymbol;
                            else
                            {
                                m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, "invalid path lookup expression: cannot infer left-hand of the path"));
                                m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, $"debug: current type context is {CurrentTypeContext}"));
                                return null;
                            }
                        }
                        else if (joined.BasePath is LayeAst.NamePathPart baseNamePart)
                        {
                            Debug.Assert(baseNamePart.Name is LayeToken.Identifier, "named typed was not an identifier");
                            string baseName = ((LayeToken.Identifier)baseNamePart.Name).Image;

                            var lookupSymbol = CurrentScope.LookupSymbol(baseName);
                            if (lookupSymbol is null)
                            {
                                m_diagnostics.Add(new Diagnostic.Error(baseNamePart.SourceSpan, $"the name `{baseName}` does not exist in the current context"));
                                return null;
                            }

                            baseSymbol = lookupSymbol;
                        }
                        else
                        {
                            m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, "invalid path lookup expression: unsupported left-hand of path"));
                            return null;
                        }

                        var variantNamePart = joined.LookupPart.Name;
                        switch (baseSymbol)
                        {
                            case Symbol.Enum enumSymbol:
                            {
                                if (variantNamePart is not LayeToken.Identifier variantNameIdent)
                                {
                                    m_diagnostics.Add(new Diagnostic.Error(variantNamePart.SourceSpan, $"non-union enums do not have a nil variant"));
                                    return null;
                                }

                                string variantName = variantNameIdent.Image;

                                Debug.Assert(enumSymbol.Type is not null, "no enum type during expression checking");
                                var enumType = enumSymbol.Type!;

                                var maybeVariant = enumType.Variants.Where(v => v.Name == variantName);
                                if (maybeVariant.Count() != 1)
                                {
                                    m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, $"enum `{enumSymbol.Name}` does not contain a variant `{variantName}`"));
                                    return null;
                                }

                                var (Name, Value) = maybeVariant.Single();
                                return new LayeCst.LoadEnumVariant(pathLookupExpr.SourceSpan, enumSymbol, Name, Value);
                            }

                            case Symbol.Union unionSymbol:
                            {
                                Debug.Assert(unionSymbol.Type is not null, "no union type during expression checking");
                                var unionType = unionSymbol.Type!;

                                if (variantNamePart is LayeToken.Identifier variantNameIdent)
                                {
                                    string variantName = variantNameIdent.Image;

                                    var maybeVariant = unionType.Variants.Where(v => v.Name == variantName);
                                    if (maybeVariant.Count() != 1)
                                    {
                                        m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, $"enum `{unionSymbol.Name}` does not contain a variant `{variantName}`"));
                                        return null;
                                    }

                                    var variant = maybeVariant.Single();
                                    if (variant.Fields.Length > 0)
                                    {
                                        m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, $"variant `{variant.Name}` in enum `{unionSymbol.Name}` requires construction"));
                                        return null;
                                    }

                                    return new LayeCst.LoadUnionVariant(pathLookupExpr.SourceSpan, unionSymbol, variant, Array.Empty<LayeCst.Expr>());
                                }
                                else
                                {
                                    Debug.Assert(variantNamePart is LayeToken.Keyword vnkw && vnkw.Kind == Keyword.Nil, "non-identifier variant was not nil");
                                    return new LayeCst.LoadUnionNilVariant(pathLookupExpr.SourceSpan, unionSymbol);
                                }
                            }

                            default:
                            {
                                m_diagnostics.Add(new Diagnostic.Error(joined.BasePath.SourceSpan, $"symbol `{baseSymbol.Name}` does not have variants"));
                                return null;
                            }
                        }
                    }

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(pathLookupExpr.SourceSpan, "invalid path lookup expression"));
                        return null;
                    }
                }
            }

            case LayeAst.NamedIndex namedIndexExpr:
            {
                var target = CheckExpression(namedIndexExpr.TargetExpression);
                if (target is null)
                {
                    AssertHasErrors("checking named index target");
                    return null;
                }

                string indexName = namedIndexExpr.Name.Image;

                bool hasDereferenced = false;
            generate_index:
                switch (target.Type)
                {
                    case SymbolType.String:
                    {
                        if (indexName == "length")
                            return new LayeCst.StringLengthLookup(namedIndexExpr.SourceSpan, target);
                        else if (indexName == "data")
                            return new LayeCst.StringDataLookup(namedIndexExpr.SourceSpan, target);

                        m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"type `string` does not contain a field named `{indexName}`"));
                        return null;
                    }

                    case SymbolType.Slice sliceType:
                    {
                        if (indexName == "length")
                            return new LayeCst.SliceLengthLookup(namedIndexExpr.SourceSpan, target);

                        m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"type `{sliceType}` does not contain a field named `{indexName}`"));
                        return null;
                    }

                    case SymbolType.Dynamic dynamicType:
                    {
                        if (indexName == "length")
                            return new LayeCst.DynamicLengthLookup(namedIndexExpr.SourceSpan, target);

                        m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"type `{dynamicType}` does not contain a field named `{indexName}`"));
                        return null;
                    }

                    case SymbolType.Struct structType:
                    {
                        var fields = structType.Fields;

                        var fieldType = fields.Where(f => f.Name == indexName).Select(f => f.Type).SingleOrDefault();
                        if (fieldType is null)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"struct `{structType.Name}` does not contain a field named `{indexName}`"));
                            return null;
                        }

                        return new LayeCst.NamedIndex(target, namedIndexExpr.Name, fieldType);
                    }

                    case SymbolType.Pointer pointerType:
                    {
                        if (hasDereferenced) goto default;
                        hasDereferenced = true;
                        target = new LayeCst.ValueAt(target, pointerType.ElementType);
                        goto generate_index;
                    }

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"cannot index type {target.Type}"));
                        return null;
                    }
                }
            }

            case LayeAst.DynamicIndex dynamicIndexExpr:
            {
                var target = CheckExpression(dynamicIndexExpr.TargetExpression);
                if (target is null)
                {
                    AssertHasErrors("checking dynamic index target");
                    return null;
                }

                var indices = new LayeCst.Expr[dynamicIndexExpr.Arguments.Length];
                for (int i = 0; i < dynamicIndexExpr.Arguments.Length; i++)
                {
                    var arg = CheckExpression(dynamicIndexExpr.Arguments[i]);
                    if (arg is null)
                    {
                        AssertHasErrors("checking dynamic index argument");
                        return null;
                    }

                    indices[i] = arg;
                }

                switch (target.Type)
                {
                    case SymbolType.Buffer bufferType:
                    {
                        if (indices.Length != 1)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"exactly one index argument required for type {target.Type}"));
                            return null;
                        }

                        LayeCst.Expr index = indices[0];
                        if (!index.Type.IsInteger() || index.Type is SymbolType.UntypedInteger)
                        {
                            var newIndex = CheckImplicitTypeCast(indices[0], SymbolTypes.UInt);
                            if (newIndex is null)
                            {
                                AssertHasErrors("checking buffer index implicit cast to uint");
                                return null;
                            }

                            index = newIndex;
                        }

                        return new LayeCst.DynamicIndex(dynamicIndexExpr.SourceSpan, target, new[] { index }, bufferType.ElementType);
                    }

                    case SymbolType.Slice sliceType:
                    {
                        if (indices.Length != 1)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"exactly one index argument required for type {target.Type}"));
                            return null;
                        }

                        LayeCst.Expr index = indices[0];
                        if (!index.Type.IsInteger() || index.Type is SymbolType.UntypedInteger)
                        {
                            var newIndex = CheckImplicitTypeCast(indices[0], SymbolTypes.UInt);
                            if (newIndex is null)
                            {
                                AssertHasErrors("checking slice index implicit cast to uint");
                                return null;
                            }

                            index = newIndex;
                        }

                        return new LayeCst.DynamicIndex(dynamicIndexExpr.SourceSpan, target, new[] { index }, sliceType.ElementType);
                    }

                    case SymbolType.Dynamic dynamicType:
                    {
                        if (indices.Length != 1)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"exactly one index argument required for type {target.Type}"));
                            return null;
                        }

                        LayeCst.Expr index = indices[0];
                        if (!index.Type.IsInteger() || index.Type is SymbolType.UntypedInteger)
                        {
                            var newIndex = CheckImplicitTypeCast(indices[0], SymbolTypes.UInt);
                            if (newIndex is null)
                            {
                                AssertHasErrors("checking dynamic index implicit cast to uint");
                                return null;
                            }

                            index = newIndex;
                        }

                        return new LayeCst.DynamicIndex(dynamicIndexExpr.SourceSpan, target, new[] { index }, dynamicType.ElementType);
                    }

                    case SymbolType.String stringType:
                    {
                        if (indices.Length != 1)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"exactly one index argument required for type {target.Type}"));
                            return null;
                        }

                        LayeCst.Expr index = indices[0];
                        if (!index.Type.IsInteger() || index.Type is SymbolType.UntypedInteger)
                        {
                            var newIndex = CheckImplicitTypeCast(indices[0], SymbolTypes.UInt);
                            if (newIndex is null)
                            {
                                AssertHasErrors("checking string index implicit cast to uint");
                                return null;
                            }

                            index = newIndex;
                        }

                        return new LayeCst.DynamicIndex(dynamicIndexExpr.SourceSpan, target, new[] { index }, SymbolTypes.U8);
                    }

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, $"cannot index type {target.Type}"));
                        return null;
                    }
                }
            }

            case LayeAst.Slice sliceExpr:
            {
                var targetExpr = CheckExpression(sliceExpr.TargetExpression);
                if (targetExpr is null)
                {
                    AssertHasErrors("checking slice target");
                    return null;
                }

                var offsetExpr = sliceExpr.OffsetExpression is null ? null : CheckExpression(sliceExpr.OffsetExpression);
                if (offsetExpr is null && sliceExpr.OffsetExpression is not null)
                {
                    AssertHasErrors("checking slice offset");
                    return null;
                }

                var countExpr = sliceExpr.CountExpression is null ? null : CheckExpression(sliceExpr.CountExpression);
                if (countExpr is null && sliceExpr.CountExpression is not null)
                {
                    AssertHasErrors("checking slice count");
                    return null;
                }

                if (offsetExpr is not null && (!offsetExpr.Type.IsInteger() || offsetExpr.Type is SymbolType.UntypedInteger))
                {
                    offsetExpr = CheckImplicitTypeCast(offsetExpr, new SymbolType.Integer(false));
                    if (offsetExpr is null)
                    {
                        AssertHasErrors("checking implicit slice offset conversion");
                        return null;
                    }
                }

                if (countExpr is not null && (!countExpr.Type.IsInteger() || countExpr.Type is SymbolType.UntypedInteger))
                {
                    countExpr = CheckImplicitTypeCast(countExpr, new SymbolType.Integer(false));
                    if (countExpr is null)
                    {
                        AssertHasErrors("checking implicit slice count conversion");
                        return null;
                    }
                }

                SymbolType elementType;
                switch (targetExpr.Type)
                {
                    case SymbolType.String: return new LayeCst.Substring(sliceExpr.SourceSpan, targetExpr, offsetExpr, countExpr);

                    case SymbolType.Array arrayType: elementType = arrayType.ElementType; break;
                    case SymbolType.Slice sliceType: elementType = sliceType.ElementType; break;

                    case SymbolType.Buffer bufferType:
                    {
                        if (countExpr is null)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(targetExpr.SourceSpan, "cannot slice buffer type without a count"));
                            return null;
                        }

                        elementType = bufferType.ElementType;
                    } break;

                    case SymbolType.Dynamic dynamicType:
                    {
                        if (countExpr is null)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(targetExpr.SourceSpan, "cannot slice dynamic type without a count"));
                            return null;
                        }

                        m_diagnostics.Add(new Diagnostic.Warning(targetExpr.SourceSpan, "since the memory of a dynamic can move during appends, slicing a dynamic creates a copy of its contents"));
                        elementType = dynamicType.ElementType;
                    } break;

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetExpr.SourceSpan, $"cannot index type {targetExpr.Type}"));
                        return null;
                    }
                }

                return new LayeCst.Slice(sliceExpr.SourceSpan, targetExpr, offsetExpr, countExpr, elementType);
            }

            case LayeAst.GroupedExpression grouped: return CheckExpression(grouped.Expression);

            case LayeAst.Cast cast:
            {
                var targetType = ResolveType(cast.TargetType);
                if (targetType is null)
                {
                    AssertHasErrors("checking cast type");
                    return null;
                }

                var targetExpression = CheckExpression(cast.TargetExpression);
                if (targetExpression is null)
                {
                    AssertHasErrors("checking cast expression");
                    return null;
                }

                return new LayeCst.TypeCast(cast.SourceSpan, targetExpression, targetType);
            }

            case LayeAst.SizeOfExpression _sizeof:
            {
                var expr = CheckExpression(_sizeof.Expression);
                if (expr is null)
                {
                    AssertHasErrors("checking sizeof expression");
                    return null;
                }

                return new LayeCst.SizeOf(_sizeof.SourceSpan, expr.Type);
            }

            case LayeAst.SizeOfType _sizeof:
            {
                switch (_sizeof._Type)
                {
                    case LayeAst.NamedType namedType:
                    {
                        if (namedType.TypePath is LayeAst.NamePathPart namePathPart)
                        {
                            Debug.Assert(namePathPart.Name is LayeToken.Identifier, "named typed was not an identifier");
                            string name = ((LayeToken.Identifier)namePathPart.Name).Image;

                            var symbol = CurrentScope.LookupSymbol(name);
                            if (symbol is null)
                            {
                                m_diagnostics.Add(new Diagnostic.Error(namePathPart.Name.SourceSpan, $"the name `{name}` does not exist in the current context"));
                                return null;
                            }

                            return new LayeCst.SizeOf(_sizeof.SourceSpan, symbol.Type!);
                        }
                    } break;
                }

                var type = ResolveType(_sizeof._Type);
                if (type is null)
                {
                    AssertHasErrors("resolving sizeof type");
                    return null;
                }

                return new LayeCst.SizeOf(_sizeof.SourceSpan, type);
            }

            case LayeAst.NameOfVariant nameofVariantExpr:
            {
                var expr = CheckExpression(nameofVariantExpr.Expression);
                if (expr is null)
                {
                    AssertHasErrors("checking nameof_variant expression");
                    return null;
                }

                switch (expr.Type)
                {
                    case SymbolType.Enum enumType: return new LayeCst.NameOfEnumVariant(nameofVariantExpr.SourceSpan, enumType, expr);
                    case SymbolType.Union unionType: return new LayeCst.NameOfUnionVariantExpression(nameofVariantExpr.SourceSpan, unionType, expr);
                    case SymbolType.UnionVariant variantType: return new LayeCst.NameOfUnionVariant(nameofVariantExpr.SourceSpan, variantType);

                    default:
                    {
                        m_diagnostics.Add(new Diagnostic.Error(nameofVariantExpr.Expression.SourceSpan, $"expression in `nameof_variant` must evaluate to a typed enum, tagged union or tagged union variant value, evaluated to {expr.Type}"));
                        return null;
                    }
                }
            }

            case LayeAst.PrefixOperation prefixExpr:
            {
                var expr = CheckExpression(prefixExpr.Expression);
                if (expr is null)
                {
                    AssertHasErrors("checking prefix expression");
                    return null;
                }

                switch (prefixExpr.Operator.Kind)
                {
                    case Operator.Subtract:
                    {
                        if (!expr.Type.IsNumeric())
                            break;

                        return new LayeCst.Negate(expr);
                    }

                    case Operator.BitAnd:
                    {
                        if (!expr.CheckIsLValue())
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expr.SourceSpan, "cannot take the address of a non l-value expression"));
                            return null;
                        }

                        return new LayeCst.AddressOf(expr);
                    }

                    case Operator.Multiply:
                    {
                        if (expr.Type is not SymbolType.Pointer pointerType)
                        {
                            m_diagnostics.Add(new Diagnostic.Error(expr.SourceSpan, "cannot dereference non-pointer expression"));
                            return null;
                        }

                        return new LayeCst.ValueAt(expr, pointerType.ElementType);
                    }
                }

                m_diagnostics.Add(new Diagnostic.Error(prefixExpr.Operator.SourceSpan, "unsupported prefix operator"));
                return null;
            }

            case LayeAst.LogicalNot not:
            {
                var expr = CheckExpression(not.Expression);
                if (expr is null)
                {
                    AssertHasErrors("checking prefix expression");
                    return null;
                }

                var boolExpr = CheckImplicitTypeCast(expr, SymbolTypes.Bool);
                if (boolExpr is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(expr.SourceSpan, "cannot perform logical not on non-bool expression"));
                    return null;
                }

                return new LayeCst.LogicalNot(boolExpr);
            }

            case LayeAst.InfixOperation infixExpr:
            {
                var leftExpr = CheckExpression(infixExpr.LeftExpression);
                if (leftExpr is null)
                {
                    AssertHasErrors("checking left infix expression");
                    return null;
                }

                var rightExpr = CheckExpression(infixExpr.RightExpression);
                if (rightExpr is null)
                {
                    AssertHasErrors("checking right infix expression");
                    return null;
                }

                switch (infixExpr.Operator.Kind)
                {
                    case Operator.Add:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.Add(leftExpr, rightExpr);
                    }

                    case Operator.Subtract:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.Subtract(leftExpr, rightExpr);
                    }

                    case Operator.Multiply:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.Multiply(leftExpr, rightExpr);
                    }

                    case Operator.Divide:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.Divide(leftExpr, rightExpr);
                    }

                    case Operator.Remainder:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.Remainder(leftExpr, rightExpr);
                    }

                    case Operator.CompareEqual:
                    {
                        if (leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric())
                        {
                            if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                            {
                                AssertHasErrors("upcasting infix expressions");
                                return null;
                            }
                        }
                        else if (leftExpr.Type is SymbolType.Union /* || leftExpr.Type is SymbolType.UnionVariant */
                            && rightExpr is LayeCst.Nil)
                        {
                            return new LayeCst.CompareUnionToNil(infixExpr.SourceSpan, leftExpr);
                        }
                        else if (leftExpr.Type != rightExpr.Type)
                            break;

                        return new LayeCst.CompareEqual(leftExpr, rightExpr);
                    }

                    case Operator.CompareNotEqual:
                    {
                        if (leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric())
                        {
                            if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                            {
                                AssertHasErrors("upcasting infix expressions");
                                return null;
                            }
                        }
                        else if (leftExpr.Type is SymbolType.Union /* || leftExpr.Type is SymbolType.UnionVariant */
                            && rightExpr is LayeCst.Nil)
                        {
                            return new LayeCst.LogicalNot(new LayeCst.CompareUnionToNil(infixExpr.SourceSpan, leftExpr));
                        }
                        else if (leftExpr.Type != rightExpr.Type)
                            break;

                        return new LayeCst.CompareNotEqual(leftExpr, rightExpr);
                    }

                    case Operator.CompareLessThan:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.CompareLess(leftExpr, rightExpr);
                    }

                    case Operator.CompareLessThanOrEqual:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.CompareLessEqual(leftExpr, rightExpr);
                    }

                    case Operator.CompareGreaterThan:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.CompareGreater(leftExpr, rightExpr);
                    }

                    case Operator.CompareGreaterThanOrEqual:
                    {
                        if (!(leftExpr.Type.IsNumeric() && rightExpr.Type.IsNumeric()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.CompareGreaterEqual(leftExpr, rightExpr);
                    }

                    case Operator.LeftShift:
                    {
                        if (!(leftExpr.Type.IsInteger() && rightExpr.Type.IsInteger()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.LeftShift(leftExpr, rightExpr);
                    }

                    case Operator.RightShift:
                    {
                        if (!(leftExpr.Type.IsInteger() && rightExpr.Type.IsInteger()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.RightShift(leftExpr, rightExpr);
                    }

                    case Operator.BitAnd:
                    {
                        if (!(leftExpr.Type.IsInteger() && rightExpr.Type.IsInteger()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.BitwiseAnd(leftExpr, rightExpr);
                    }

                    case Operator.BitOr:
                    {
                        if (!(leftExpr.Type.IsInteger() && rightExpr.Type.IsInteger()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.BitwiseOr(leftExpr, rightExpr);
                    }

                    case Operator.BitXor:
                    {
                        if (!(leftExpr.Type.IsInteger() && rightExpr.Type.IsInteger()))
                            break;

                        if (!CheckImplicitNumericUpcast(ref leftExpr, ref rightExpr))
                        {
                            AssertHasErrors("upcasting infix expressions");
                            return null;
                        }

                        return new LayeCst.BitwiseXor(leftExpr, rightExpr);
                    }
                }

                m_diagnostics.Add(new Diagnostic.Error(infixExpr.Operator.SourceSpan, $"unsupported infix operator on types {leftExpr.Type} and {rightExpr.Type}"));
                return null;
            }

            case LayeAst.LogicalInfixOperation logicalInfixExpr:
            {
                var leftExpr = CheckExpression(logicalInfixExpr.LeftExpression);
                if (leftExpr is null)
                {
                    AssertHasErrors("checking left logical infix expression");
                    return null;
                }

                leftExpr = CheckImplicitTypeCast(leftExpr, SymbolTypes.Bool);
                if (leftExpr is null)
                {
                    AssertHasErrors("checking left logical infix expression conversion to bool");
                    return null;
                }

                var rightExpr = CheckExpression(logicalInfixExpr.RightExpression);
                if (rightExpr is null)
                {
                    AssertHasErrors("checking right logical infix expression");
                    return null;
                }

                rightExpr = CheckImplicitTypeCast(rightExpr, SymbolTypes.Bool);
                if (rightExpr is null)
                {
                    AssertHasErrors("checking right logical infix expression conversion to bool");
                    return null;
                }

                if (logicalInfixExpr.Keyword.Kind == Keyword.And)
                    return new LayeCst.LogicalAnd(leftExpr, rightExpr);
                else if (logicalInfixExpr.Keyword.Kind == Keyword.Or)
                    return new LayeCst.LogicalOr(leftExpr, rightExpr);

                m_diagnostics.Add(new Diagnostic.Error(logicalInfixExpr.Keyword.SourceSpan, $"internal compiler error: unhandled logical infix keyword"));
                return null;
            }

            case LayeAst.Invoke invokeExpr: return CheckInvoke(invokeExpr);

            default:
            {
                Console.WriteLine($"internal compiler error: unrecognized expression type ({expression.GetType().Name})");
                Environment.Exit(1);
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
#if false
                    else if (arg.Type is SymbolType.SizedFloat sfloat && sfloat.BitCount < 64)
                        argValues[i] = new LayeCst.TypeCast(arg.SourceSpan, arg, new SymbolType.SizedFloat(64));
#endif
                }
            }

            return new LayeCst.InvokeFunction(invoke.SourceSpan, fnSymbol, argValues.ToArray());
        }
        else if (invoke.TargetExpression is LayeAst.PathLookup pathLookup && pathLookup.Path is LayeAst.JoinedPath joinedPath)
        {
            if (joinedPath.LookupPart.Name is not LayeToken.Identifier)
            {
                m_diagnostics.Add(new Diagnostic.Error(joinedPath.LookupPart.SourceSpan, $"can only construct namedd variants"));
                return null;
            }

            Symbol.Union unionSymbol;

            if (joinedPath.BasePath is LayeAst.NamePathPart enumTypeNamePart)
            {
                Debug.Assert(enumTypeNamePart.Name is LayeToken.Identifier, "named typed was not an identifier");
                string name = ((LayeToken.Identifier)enumTypeNamePart.Name).Image;

                var lookupSymbol = CurrentScope.LookupSymbol(name);
                if (lookupSymbol is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(enumTypeNamePart.SourceSpan, $"the name `{name}` does not exist in the current context"));
                    return null;
                }

                if (lookupSymbol is not Symbol.Union checkSymbol)
                {
                    m_diagnostics.Add(new Diagnostic.Error(enumTypeNamePart.SourceSpan, $"`{name}` must be a tagged union to construct a tagged union variant"));
                    return null;
                }

                unionSymbol = checkSymbol;
            }
            else if (joinedPath.BasePath is LayeAst.EmptyPathPart emptyPart)
            {
                var typeContext = CurrentTypeContext;
                if (typeContext is null || typeContext is not SymbolType.Union checkType)
                {
                    m_diagnostics.Add(new Diagnostic.Error(emptyPart.SourceSpan, $"`unable to infer the type of union to construct"));
                    return null;
                }

                unionSymbol = checkType.UnionSymbol;
            }
            else
            {
                m_diagnostics.Add(new Diagnostic.Error(invoke.TargetExpression.SourceSpan, "can't invoke this"));
                return null;
            }

            Debug.Assert(joinedPath.LookupPart.Name is LayeToken.Identifier, "named typed was not an identifier");
            string lookupName = ((LayeToken.Identifier)joinedPath.LookupPart.Name).Image;

            var maybeVariant = unionSymbol.Type!.Variants.Where(v => v.Name == lookupName);
            if (maybeVariant.Count() != 1)
            {
                m_diagnostics.Add(new Diagnostic.Error(joinedPath.LookupPart.SourceSpan, $"enum `{unionSymbol.Name}` does not contain a variant `{lookupName}`"));
                return null;
            }

            var variant = maybeVariant.Single();

            if (variant.Fields.Length != argValues.Length)
            {
                m_diagnostics.Add(new Diagnostic.Error(joinedPath.LookupPart.SourceSpan, $"variant `{lookupName}` in enum `{unionSymbol.Name}` requires exactly {variant.Fields.Length} arguments, got {argValues.Length}"));
                return null;
            }

            for (int i = 0; i < argValues.Length; i++)
            {
                var arg = argValues[i];
                var param = variant.Fields[i];

                if (CheckImplicitTypeCast(arg, param.Type) is not LayeCst.Expr argCast)
                {
                    AssertHasErrors("failing to convert argument to the correct type");
                    return null;
                }

                argValues[i] = argCast;
            }

            return new LayeCst.LoadUnionVariant(invoke.SourceSpan, unionSymbol, variant, argValues);
        }
        else
        {
            m_diagnostics.Add(new Diagnostic.Error(invoke.TargetExpression.SourceSpan, "can't invoke this"));
            return null;
        }
    }

    private LayeCst.Expr? CheckImplicitTypeCast(LayeCst.Expr value, SymbolType targetType)
    {
        if (value.Type == targetType)
            return value;

        switch (value.Type)
        {
            case SymbolType.RawPtr:
            {
                if (targetType is SymbolType.Buffer buffer)
                {
                    if (value is LayeCst.NullPtr nullptr)
                        return new LayeCst.NullPtr(nullptr.Literal, targetType, buffer.ElementType);

                    return new LayeCst.TypeCast(value.SourceSpan, value, targetType);
                }
                else if (targetType is SymbolType.Pointer pointer)
                {
                    if (value is LayeCst.NullPtr nullptr)
                        return new LayeCst.NullPtr(nullptr.Literal, targetType, pointer.ElementType);

                    return new LayeCst.TypeCast(value.SourceSpan, value, targetType);
                }
            } break;

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

                    return new LayeCst.TypeCast(value.SourceSpan, value, SymbolTypes.RawPtr);
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
                if (targetType is SymbolType.RawPtr)
                    return new LayeCst.TypeCast(value.SourceSpan, value, targetType);
                else if (targetType is SymbolType.Pointer _targetPointerType && _targetPointerType.ElementType == pointerType.ElementType)
                {
                    if (pointerType.Access == AccessKind.ReadWrite)
                        return new LayeCst.TypeCast(value.SourceSpan, value, pointerType);
                }
            } break;

            case SymbolType.Buffer bufferType:
            {
                if (targetType is SymbolType.RawPtr)
                    return new LayeCst.TypeCast(value.SourceSpan, value, SymbolTypes.RawPtr);
                else if (targetType is SymbolType.Buffer _targetBufferType && _targetBufferType.ElementType == bufferType.ElementType)
                {
                    if (bufferType.Access == AccessKind.ReadWrite)
                        return new LayeCst.TypeCast(value.SourceSpan, value, bufferType);
                }
            } break;

            case SymbolType.Slice sliceType:
            {
                if (targetType is SymbolType.Slice _targetSliceType && _targetSliceType.ElementType == sliceType.ElementType)
                {
                    if (sliceType.Access == AccessKind.ReadWrite)
                        return new LayeCst.TypeCast(value.SourceSpan, value, sliceType);
                }
                else if (targetType is SymbolType.String)
                    return new LayeCst.SliceToString(value);
            } break;

            case SymbolType.UnionVariant variantType:
            {
                if (targetType is SymbolType.Union parentUnionType && parentUnionType.Variants.Contains(variantType))
                    return new LayeCst.UnionVariantDowncast(value, parentUnionType);
            } break;
        }

        m_diagnostics.Add(new Diagnostic.Error(value.SourceSpan, $"unable to convert from {value.Type} to {targetType}"));
        return null;
    }

    private bool CheckImplicitNumericUpcast(ref LayeCst.Expr left, ref LayeCst.Expr right)
    {
        bool shouldUpcastToFloats = left.Type is SymbolType.UntypedFloat || left.Type is SymbolType.Float /* || left.Type is SymbolType.SizedFloat */
            || right.Type is SymbolType.UntypedFloat || right.Type is SymbolType.Float /* || right.Type is SymbolType.SizedFloat */;

        if (shouldUpcastToFloats)
            return CheckImplicitFloatUpcast_Impl(ref left, ref right);

        // Both untyped
        if (left.Type is SymbolType.UntypedInteger _lUntyped0 && right.Type is SymbolType.UntypedInteger _rUntyped0)
        {
            var leftLit = (LayeCst.Integer)left;
            left = new LayeCst.Integer(leftLit.Literal, _lUntyped0.Signed ? SymbolTypes.Int : SymbolTypes.UInt);

            var rightLit = (LayeCst.Integer)right;
            right = new LayeCst.Integer(rightLit.Literal, _rUntyped0.Signed ? SymbolTypes.Int : SymbolTypes.UInt);

            return true;
        }

        // One of them is untyped, but not both
        if (left.Type is SymbolType.UntypedInteger)
            return CheckImplicitIntegerUpcast_Impl(ref left, right);
        else if (right.Type is SymbolType.UntypedInteger)
            return CheckImplicitIntegerUpcast_Impl(ref right, left);

        if (left.Type == right.Type) return true;

        if (left.Type is SymbolType.Integer && right.Type is SymbolType.Integer)
        {
            m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(left, right), "unable to convert expressions to the same numeric type"));
            return false;
        }

        bool swap = false;

        // if left is uint, it's the dominant by default
        if (left.Type is SymbolType.Integer)
            swap = true;
        else
        {
            // if right is NOT sized, it's the dominant and we shouldn't swap, so don't check
            var _lSized1 = (SymbolType.SizedInteger)left.Type;

            if (right.Type is SymbolType.SizedInteger _rSized1)
            {
                if (_lSized1.BitCount > _rSized1.BitCount)
                    swap = true;
                else if (_lSized1.BitCount == _rSized1.BitCount)
                {
                    m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(left, right), "unable to convert expressions to the same numeric type"));
                    return false;
                }
            }
        }

        if (swap)
            return CheckImplicitIntegerUpcast_Impl(ref right, left);
        else return CheckImplicitIntegerUpcast_Impl(ref left, right);
    }

    private bool CheckImplicitFloatUpcast_Impl(ref LayeCst.Expr left, ref LayeCst.Expr right)
    {
        bool leftIsInteger = left.Type is SymbolType.UntypedInteger || left.Type is SymbolType.Integer || left.Type is SymbolType.SizedInteger;
        bool rightIsInteger = right.Type is SymbolType.UntypedInteger || right.Type is SymbolType.Integer || right.Type is SymbolType.SizedInteger;

        if (left.Type is SymbolType.UntypedFloat)
        {
            var leftLit = (LayeCst.Float)left;
            left = new LayeCst.Float(leftLit.Literal, SymbolTypes.Float);
        }
        else if (left.Type is SymbolType.UntypedInteger)
        {
            var leftLit = (LayeCst.Integer)left;
            left = new LayeCst.Float(new(leftLit.SourceSpan, leftLit.Literal.LiteralValue), SymbolTypes.Float);
        }
        else if (left.Type is not SymbolType.Float)
        {
            if (!leftIsInteger)
            {
                m_diagnostics.Add(new Diagnostic.Error(left.SourceSpan, "expression cannot be made numeric"));
                return false;
            }

            left = new LayeCst.TypeCast(left.SourceSpan, left, SymbolTypes.Float);
        }

        if (right.Type is SymbolType.UntypedFloat)
        {
            var rightLit = (LayeCst.Float)right;
            right = new LayeCst.Float(rightLit.Literal, SymbolTypes.Float);
        }
        else if (right.Type is SymbolType.UntypedInteger)
        {
            var rightLit = (LayeCst.Integer)right;
            right = new LayeCst.Float(new(rightLit.SourceSpan, rightLit.Literal.LiteralValue), SymbolTypes.Float);
        }
        else if (right.Type is not SymbolType.Float)
        {
            if (!rightIsInteger)
            {
                m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, "expression cannot be made numeric"));
                return false;
            }

            right = new LayeCst.TypeCast(right.SourceSpan, right, SymbolTypes.Float);
        }

        return true;
    }

    private bool CheckImplicitIntegerUpcast_Impl(ref LayeCst.Expr left, LayeCst.Expr right)
    {
        // at this point, right will NEVER be untyped

        if (left.Type is SymbolType.UntypedInteger _lUntyped1)
        {
            var leftLit = (LayeCst.Integer)left;
            if (right.Type is SymbolType.Integer _rInteger1)
            {
                if (_lUntyped1.Signed && (long)leftLit.Literal.LiteralValue < 0)
                {
                    if (!_rInteger1.Signed)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, "left integer literal is negative: cannot convert to unsigned integer type"));
                        return false;
                    }

                    ulong literalPositive = (ulong)-(long)leftLit.Literal.LiteralValue;
                    if (literalPositive > (ulong)-(long.MinValue + 1) + 1)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, $"left integer literal is out of range of the signed platform integer (64 bits, min value {long.MinValue})"));
                        return false;
                    }
                }
                else
                {
                    // else treat it as unsigned

                    if (_rInteger1.Signed && leftLit.Literal.LiteralValue > long.MaxValue)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, $"left integer literal is out of range of the signed platform integer (64 bits, max value {long.MaxValue})"));
                        return false;
                    }
                }

                // here, the literal can be stored in the right type
                left = new LayeCst.Integer(leftLit.Literal, right.Type);
                return true;
            }

            // here, right is a sized integer
            var _rSized1 = (SymbolType.SizedInteger)right.Type;

            if (_lUntyped1.Signed && (long)leftLit.Literal.LiteralValue < 0)
            {
                if (!_rSized1.Signed)
                {
                    m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, "left integer literal is negative: cannot convert to unsigned integer type"));
                    return false;
                }

                ulong literalPositive = (ulong)-(long)leftLit.Literal.LiteralValue;
                if (literalPositive > AbsSignedMinForBits(_rSized1.BitCount))
                {
                    m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, $"left integer literal is out of range of the signed platform integer (64 bits, min value -{AbsSignedMinForBits(_rSized1.BitCount)})"));
                    return false;
                }
            }
            else
            {
                // else treat it as unsigned

                if (_rSized1.Signed && leftLit.Literal.LiteralValue > SignedMaxForBits(_rSized1.BitCount))
                {
                    m_diagnostics.Add(new Diagnostic.Error(right.SourceSpan, $"left integer literal is out of range of the signed platform integer (64 bits, max value {SignedMaxForBits(_rSized1.BitCount)})"));
                    return false;
                }
            }

            // here, the literal can be stored in the right type
            left = new LayeCst.Integer(leftLit.Literal, right.Type);
            return true;
        }

        // Otherwise, both are typed

        // We should never get called if both types are unsized
        // left should be sized and right COULD be unsized

        // we should also not get here if the bit width is the same but the sign is different

        // given the above, I think every other case is handled and we can just cast

        left = new LayeCst.TypeCast(left.SourceSpan, left, right.Type);
        return true;

        //m_diagnostics.Add(new Diagnostic.Error(SourceSpan.Combine(left, right), "unable to convert expressions to the same numeric type"));
        //return false;
    }

    private static ulong AbsSignedMinForBits(uint bitCount)
    {
        if (bitCount > 64)
            throw new NotImplementedException();

        if (bitCount == 64) return (ulong)-(long.MinValue + 1) + 1;
        if (bitCount == 32) return (ulong)-(long)int.MinValue;
        if (bitCount == 16) return (ulong)-(long)short.MinValue;
        if (bitCount == 8) return (ulong)-(long)sbyte.MinValue;

        throw new NotImplementedException();
    }

    private static ulong SignedMaxForBits(uint bitCount)
    {
        if (bitCount > 64)
            throw new NotImplementedException();

        if (bitCount == 64) return long.MaxValue;
        if (bitCount == 32) return (ulong)int.MaxValue;
        if (bitCount == 16) return (ulong)short.MaxValue;
        if (bitCount == 8) return (ulong)sbyte.MaxValue;

        throw new NotImplementedException();
    }
}
