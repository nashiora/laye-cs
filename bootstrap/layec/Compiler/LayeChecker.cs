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

                        m_symbols.AddSymbol(sym);
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
}
