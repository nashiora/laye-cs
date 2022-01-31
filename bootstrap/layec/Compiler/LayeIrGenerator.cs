using System.Diagnostics;

namespace laye.Compiler;

internal class LayeIrGenerator
{
    public static LayeIrModule? GenerateIr(LayeCstRoot[] syntax, List<Diagnostic> diagnostics)
    {
        var checker = new LayeIrGenerator(syntax, diagnostics);
        return checker.CreateModule();
    }

    private readonly LayeCstRoot[] m_syntax;

    private readonly List<Diagnostic> m_diagnostics;
    private readonly int m_originalErrorCount;

    private LayeIrGenerator(LayeCstRoot[] syntax, List<Diagnostic> diagnostics)
    {
        m_syntax = syntax;
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
        var irTypes = new List<Symbol>();
        var irFunctions = new List<LayeIr.Function>();

        // compile functions to ir
        foreach (var astRoot in m_syntax)
        {
            var topLevelNodes = astRoot.TopLevelNodes;
            foreach (var node in topLevelNodes)
            {
                switch (node)
                {
                    case LayeCst.FunctionDeclaration fnDecl:
                    {
                        var fn = GenerateFunction(fnDecl);
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

    private LayeIr.Function? GenerateFunction(LayeCst.FunctionDeclaration fnDecl)
    {
        var functionBuilder = new LayeIrFunctionBuilder(fnDecl.FunctionName, fnDecl.FunctionSymbol);

        switch (fnDecl.Body)
        {
            case LayeCst.EmptyFunctionBody: return functionBuilder.Build();

            case LayeCst.BlockFunctionBody blockFunctionBody:
            {
                var entryBlock = functionBuilder.AppendBasicBlock();
                functionBuilder.PositionAtEnd(entryBlock);

                foreach (var childNode in blockFunctionBody.BodyBlock.Body)
                {
                    if (!GenerateStatementIr(functionBuilder, childNode))
                    {
                        AssertHasErrors("failing to check statement in function body block");
                        return null;
                    }
                }
            } break;
        }

        // TODO(local): validate before build (unterminated blocks will throw, so instead report errors before attempting to build)

        if (fnDecl.FunctionSymbol.Type!.ReturnType is SymbolType.Void)
        {
            foreach (var block in functionBuilder.BasicBlocks)
            {
                if (block.TerminatorInstruction is null)
                    block.TerminatorInstruction = new LayeIr.ReturnVoid(fnDecl.FunctionName.SourceSpan);
            }
        }
        else
        {
            foreach (var block in functionBuilder.BasicBlocks)
            {
                if (block.TerminatorInstruction is null)
                {
                    m_diagnostics.Add(new Diagnostic.Error(fnDecl.FunctionName.SourceSpan, "not all code paths return a value"));
                    return null;
                }
            }
        }

        return functionBuilder.Build();
    }

    private bool GenerateStatementIr(LayeIrFunctionBuilder builder, LayeCst.Stmt statement)
    {
        switch (statement)
        {
            case LayeCst.ExpressionStatement exprStmt: return GenerateExpressionIr(builder, exprStmt.Expression) is not null;

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(statement.SourceSpan, "unrecognized statement type"));
                return false;
            }
        }
    }

    private LayeIr.Value? GenerateExpressionIr(LayeIrFunctionBuilder builder, LayeCst.Expr expression)
    {
        switch (expression)
        {
            //case LayeAst.NameLookup nameLookupExpr: { }

            case LayeCst.Integer intExpr: return builder.BuildInteger(intExpr.Literal.SourceSpan, intExpr.Literal.LiteralValue, intExpr.Type);
            case LayeCst.Float floatExpr: return builder.BuildFloat(floatExpr.Literal.SourceSpan, floatExpr.Literal.LiteralValue, floatExpr.Type);
            case LayeCst.String stringExpr: return builder.BuildString(stringExpr.Literal.SourceSpan, stringExpr.Literal.LiteralValue, stringExpr.Type);

            case LayeCst.TypeCast typeCastExpr:
            {
                var value = GenerateExpressionIr(builder, typeCastExpr.Expression);
                if (value is null)
                {
                    AssertHasErrors("failing to generate cast inner expression");
                    return null;
                }
                // TODO(local): allow other casts
                return builder.BuildIntToRawPtrCast(value);
            }

            case LayeCst.InvokeFunction invokeFunctionExpr: return CheckInvokeFunction(builder, invokeFunctionExpr);

            default:
            {
                m_diagnostics.Add(new Diagnostic.Error(expression.SourceSpan, "unrecognized expression type"));
                return null;
            }
        }
    }

    private LayeIr.Value? CheckInvokeFunction(LayeIrFunctionBuilder builder, LayeCst.InvokeFunction invoke)
    {
        var argValues = new List<LayeIr.Value>();
        foreach (var arg in invoke.Arguments)
        {
            var argValue = GenerateExpressionIr(builder, arg);
            if (argValue is null)
            {
                AssertHasErrors("failing to check invocation argument");
                return null;
            }

            argValues.Add(argValue);
        }

        return builder.BuildInvokeGlobalFunction(invoke.SourceSpan, invoke.TargetFunctionSymbol, argValues.ToArray());
    }
}
