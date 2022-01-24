using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace laye.Compiler;

internal sealed class LayeChecker
{
    public static LayeIrModule CheckSyntax(LayeAstRoot[] syntax, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        var checker = new LayeChecker(syntax, symbols, diagnostics);
        return checker.CreateModule();
    }

    private readonly LayeAstRoot[] m_syntax;
    private readonly SymbolTable m_symbols;

    private readonly List<Diagnostic> m_diagnostics;
    private readonly int m_originalErrorCount;

    private LayeChecker(LayeAstRoot[] syntax, SymbolTable symbols, List<Diagnostic> diagnostics)
    {
        m_syntax = syntax;
        m_symbols = symbols;
        m_diagnostics = diagnostics;

        m_originalErrorCount = diagnostics.Count(d => d is Diagnostic.Error);
    }

    private void AssertHasErrors(string context)
    {
        int errorCount = m_diagnostics.Count(d => d is Diagnostic.Error) - m_originalErrorCount;
        Debug.Assert(errorCount > 0, $"No error diagnostics generated when {context}");
    }

    private LayeIrModule? CreateModule()
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

                        if (!m_symbols.AddSymbol(sym))
                        {
                            m_diagnostics.Add(new Diagnostic.Error(fnDecl.Name.SourceSpan, $"`{sym.Name}` is already define in this scope (function overloading is not supported)"));
                            return null;
                        }
                    } break;
                }
            }
        }

        var irTypes = new List<Symbol>();

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
                            return null;
                        }

                        var paramTypes = new List<SymbolType>();
                        foreach (var paramAstType in fnDecl.Parameters)
                        {
                            var paramType = ResolveType(paramAstType.Binding.BindingType);
                            if (paramType is null)
                            {
                                AssertHasErrors("failing to resolve function parameter type in symbol creation");
                                return null;
                            }

                            paramTypes.Add(paramType);
                        }

                        var ccKind = CallingConvention.None;
                        var vaKind = fnDecl.VarArgsKind;

                        sym.Type = new SymbolType.Function(fnDecl.Name.Image, Array.Empty<TypeParam>(), ccKind, returnType, paramTypes.ToArray(), vaKind);
                    } break;
                }
            }
        }

        var irFunctions = new List<LayeIr.Function>();

        // compile functions to ir
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

                        var fn = CheckFunction(fnDecl, sym);
                        if (fn is null)
                        {
                            AssertHasErrors("failing to compile function");
                            return null;
                        }

                        irFunctions.Add(fn);
                    } break;
                }
            }
        }

        return new LayeIrModule(irFunctions.ToArray(), irTypes.ToArray());
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
                    //case Keyword.SizedBool: return new SymbolType.SizedBool(kw.SizeData);

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

    private LayeIr.Function? CheckFunction(LayeAst.FunctionDeclaration fnDecl, Symbol.Function sym)
    {
        var functionBuilder = new LayeIrFunctionBuilder(fnDecl.Name, sym);

        switch (fnDecl.Body)
        {
            case LayeAst.EmptyFunctionBody: return functionBuilder.Build();

            case LayeAst.BlockFunctionBody blockFunctionBody:
            {
                var entryBlock = functionBuilder.AppendBasicBlock();
                functionBuilder.PositionAtEnd(entryBlock);

                foreach (var childNode in blockFunctionBody.BodyBlock.Body)
                {
                    if (!CheckStatement(functionBuilder, childNode))
                    {
                        AssertHasErrors("failing to check statement in function body block");
                        return null;
                    }
                }
            } break;
        }

        // TODO(local): validate before build (unterminated blocks will throw, so instead report errors before attempting to build)

        if (sym.Type!.ReturnType is SymbolType.Void)
        {
            foreach (var block in functionBuilder.BasicBlocks)
            {
                if (block.TerminatorInstruction is null)
                    block.TerminatorInstruction = new LayeIr.ReturnVoid(fnDecl.Name.SourceSpan);
            }
        }
        else
        {
            foreach (var block in functionBuilder.BasicBlocks)
            {
                if (block.TerminatorInstruction is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(fnDecl.Name.SourceSpan, "not all code paths return a value"));
                    return null;
                }
            }
        }

        return functionBuilder.Build();
    }

    private bool CheckStatement(LayeIrFunctionBuilder builder, LayeAst.Stmt statement)
    {
        switch (statement)
        {
            case LayeAst.ExpressionStatement exprStmt: return CheckExpression(builder, exprStmt.Expression) is not null;

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "unrecognized statement type"));
                return false;
            }
        }
    }

    private LayeIr.Value? CheckExpression(LayeIrFunctionBuilder builder, LayeAst.Expr statement)
    {
        switch (statement)
        {
            case LayeAst.Integer intExpr: return builder.BuildInteger(intExpr.Literal.SourceSpan, intExpr.Literal.LiteralValue);
            case LayeAst.String stringExpr: return builder.BuildString(stringExpr.Literal.SourceSpan, stringExpr.Literal.LiteralValue);

            case LayeAst.Invoke invokeExpr: return CheckInvoke(builder, invokeExpr);

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "unrecognized expression type"));
                return null;
            }
        }
    }

    private LayeIr.Value? CheckInvoke(LayeIrFunctionBuilder builder, LayeAst.Invoke invoke)
    {
        var argValues = new List<LayeIr.Value>();

        foreach (var arg in invoke.Arguments)
        {
            var argValue = CheckExpression(builder, arg);
            if (argValue is null)
            {
                AssertHasErrors("failing to check invocation argument");
                return null;
            }

            argValues.Add(argValue);
        }

        if (invoke.TargetExpression is LayeAst.NameLookup targetNameLookup)
        {
            string targetName = targetNameLookup.Image;
            if (!m_symbols.TryGetSymbol(targetName, out var symbol))
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
            var targetParamTypes = fnSymbol.Type.ParameterTypes;

            switch (fnSymbol.Type.VarArgs)
            {
                case VarArgsKind.None:
                {
                    if (targetParamTypes.Length != argValues.Count)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected {targetParamTypes.Length} arguments to function `{targetName}`, got {argValues.Count}"));
                        return null;
                    }
                } break;

                case VarArgsKind.C:
                {
                    if (targetParamTypes.Length > argValues.Count)
                    {
                        m_diagnostics.Add(new Diagnostic.Error(targetNameLookup.SourceSpan, $"expected at least {targetParamTypes.Length} arguments to C-style varargs function `{targetName}`, got {argValues.Count}"));
                        return null;
                    }
                } break;

                default: throw new NotImplementedException($"varargs kind not handled in checker");
            }

            return builder.BuildInvokeGlobalFunction(invoke.SourceSpan, fnSymbol, argValues.ToArray());
        }
        else
        {
            m_diagnostics.Add(new Diagnostic.Error(invoke.TargetExpression.SourceSpan, "can only invoke top level functions"));
            return null;
        }
    }
}
