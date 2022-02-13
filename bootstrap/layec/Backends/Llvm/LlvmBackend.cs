using System.Diagnostics;

using LLVMSharp;
using static LLVMSharp.LLVM;

using static CompilerStatus;

namespace laye.Backends.Llvm;

internal sealed class LlvmBackend : IBackend
{
    public LLVMContextRef Context;
    public LLVMModuleRef Module;

#if false
    public readonly SymbolType.Integer IntType = new(true);
    public readonly SymbolType.Integer UIntType = new(false);

    public readonly SymbolType.SizedInteger Int32Type = new(true, 32);
    public readonly SymbolType.SizedInteger Int64Type = new(true, 64);
    public readonly SymbolType.SizedInteger UInt64Type = new(false, 64);

    public readonly SymbolType.String StringType = new();

    public readonly SymbolType.Pointer U8PtrType = new(new SymbolType.SizedInteger(false, 8));
    public readonly SymbolType.Buffer ReadOnlyU8BufType = new(new SymbolType.SizedInteger(false, 8), AccessKind.ReadOnly);
    public readonly SymbolType.Slice U8SlcType = new(new SymbolType.SizedInteger(false, 8));
    public readonly SymbolType.Slice ReadOnlyU8SlcType = new(new SymbolType.SizedInteger(false, 8), AccessKind.ReadOnly);
#endif

    private readonly Dictionary<Symbol.Function, LLVMValueRef> m_functions = new();

    private readonly HashSet<string> m_externLibraryReferences = new();

    public int Compile(LayeCstRoot[] roots, BackendOptions options)
    {
        ShowInfo("Compiling using LLVM backend");

        var irCompilerTimer = Stopwatch.StartNew();

        string lmoduleName = Path.GetFileNameWithoutExtension(options.OutputFileName);

        Context = ContextCreate();
        Module = ModuleCreateWithNameInContext(lmoduleName, Context);

        ShowVerbose("Declaring LLVM functions");
        foreach (var root in roots)
        {
            foreach (var node in root.TopLevelNodes)
            {
                switch (node)
                {
                    case LayeCst.FunctionDeclaration function:
                        m_functions[function.FunctionSymbol] = DeclareFunction(function);
                        break;
                }
            }
        }

        ShowVerbose("Compiling functions to LLVM IR");
        foreach (var root in roots)
        {
            foreach (var node in root.TopLevelNodes)
            {
                switch (node)
                {
                    case LayeCst.FunctionDeclaration function:
                        if (function.Body is not LayeCst.EmptyFunctionBody)
                            CompileFunction(function);
                        break;
                }
            }
        }

        var passManager = LLVM.CreateFunctionPassManagerForModule(Module);
        //LLVM.AddInstructionCombiningPass(passManager);
        //LLVM.InitializeFunctionPassManager(passManager);

        foreach (var function in m_functions.Values)
            LLVM.RunFunctionPassManager(passManager, function);

        irCompilerTimer.Stop();

        var irCompilerElapsedTime = irCompilerTimer.Elapsed;
        ShowInfo($"Compiled to LLVM IR in {irCompilerElapsedTime.TotalSeconds:N2}s");

        ShowVerbose("Writing IR and bitcode to temporary files");

        var tempFileWriterTimer = Stopwatch.StartNew();

        //if (moduleParams.Kind == ModuleKind.Console || moduleParams.Kind == ModuleKind.Windows)
        //Debug.Assert(asmDef.EntryPoint is not null, "no program entry point specified for executable, program checker failed");

        string llFileName = Path.ChangeExtension(options.OutputFileName, ".ll");
        string bcFileName = Path.ChangeExtension(options.OutputFileName, ".bc");

        LLVMBool printResult = PrintModuleToFile(Module, llFileName, out string printError);
        if (printResult.Value != 0)
        {
            Console.WriteLine(printError);
            return 1;
        }

        int bcWriteResult = WriteBitcodeToFile(Module, bcFileName);
        if (bcWriteResult != 0)
        {
            Console.WriteLine("Failed to generate LLVM bitcode file.");
            return 1;
        }

        tempFileWriterTimer.Stop();

        var tempFileWriterElapsedTime = tempFileWriterTimer.Elapsed;
        ShowVerbose($"Wrote temp files in {tempFileWriterElapsedTime.TotalSeconds:N2}s");

        string outFileName = options.OutputFileName;
        //outFileName = Path.ChangeExtension(outFileName, ".o");

        ShowInfo($"Generating output file `{outFileName}` with clang");

        var outFileWriterTimer = Stopwatch.StartNew();

        IEnumerable<string> filesToLinkAgainst = options.FilesToLinkAgainst.Concat(m_externLibraryReferences.Select(ex => $"-l{ex}"));

        // i686-pc-windows-gnu
        string additionalFiles = string.Join(" ", filesToLinkAgainst.Select(f => $"\"{f}\""));
        string clangArguments = $"-target {options.TargetTriple} \"{bcFileName}\" {additionalFiles} -g -v \"-o{outFileName}\"";
        if (options.IsExecutable)
        { }// clangArguments += " -Wl,-ENTRY:_laye_start";
        else clangArguments += " -shared -fuse-ld=llvm-lib";

        if (options.AdditionalArguments.Length > 0)
        {
            string additionalArguments = string.Join(" ", options.AdditionalArguments.Select(a => $"\"{a}\""));
            clangArguments += $" {additionalArguments}";
        }

        ShowCommand($"clang {clangArguments}");

        var clangProcessStartInfo = new ProcessStartInfo()
        {
            FileName = "clang",
            Arguments = clangArguments,
        };

        if (!options.ShowBackendOutput)
        {
            clangProcessStartInfo.RedirectStandardInput = false;
            clangProcessStartInfo.RedirectStandardOutput = false;
            clangProcessStartInfo.RedirectStandardError = false;

            clangProcessStartInfo.UseShellExecute = false;
            clangProcessStartInfo.CreateNoWindow = true;
        }

        var clangProcess = new Process()
        {
            StartInfo = clangProcessStartInfo,
        };

        clangProcess.Start();
        clangProcess.WaitForExit();

        if (!options.KeepTemporaryFiles)
        {
            string ilkFileName = Path.ChangeExtension(options.OutputFileName, ".ilk");
            string pdbFileName = Path.ChangeExtension(options.OutputFileName, ".pdb");
            if (File.Exists(llFileName)) File.Delete(llFileName);
            if (File.Exists(bcFileName)) File.Delete(bcFileName);
            if (File.Exists(ilkFileName)) File.Delete(ilkFileName);
            if (File.Exists(pdbFileName)) File.Delete(pdbFileName);
        }

        outFileWriterTimer.Stop();

        if (clangProcess.ExitCode == 0)
        {
            var outFileWriterElapsedTime = outFileWriterTimer.Elapsed;
            ShowInfo($"Generated output file in {outFileWriterElapsedTime.TotalSeconds:N2}s");
        }
        else ShowInfo($"Failed to generate output file (clang exited with code {clangProcess.ExitCode})");

        return clangProcess.ExitCode;
    }

    public LLVMValueRef GetFunctionValueFromSymbol(Symbol.Function function) => m_functions[function];

    private readonly Dictionary<SymbolType, LLVMTypeRef> m_typeCache = new();
    public LLVMTypeRef GetLlvmType(SymbolType type)
    {
        if (!m_typeCache.TryGetValue(type, out var llvmType))
            m_typeCache[type] = llvmType = GetLlvmType_Impl(type);

        return llvmType;
    }

    private LLVMTypeRef GetLlvmType_Impl(SymbolType type)
    {
        switch (type)
        {
            case SymbolType.Void: return VoidTypeInContext(Context);
            case SymbolType.Rune: return Int32TypeInContext(Context);

            //case SymbolType.Bool: return GetLlvmType(SymbolTypes.UInt);
            case SymbolType.Bool: return Int1TypeInContext(Context);
            case SymbolType.SizedBool _bx: return IntTypeInContext(Context, _bx.BitCount);

            case SymbolType.Integer: return Int64TypeInContext(Context);
            case SymbolType.SizedInteger _ix: return IntTypeInContext(Context, _ix.BitCount);

            case SymbolType.Float: return DoubleTypeInContext(Context);
#if false
            case SymbolType.SizedFloat _fx:
            {
                if (_fx.BitCount == 16)
                    return HalfTypeInContext(Context);
                else if (_fx.BitCount == 32)
                    return FloatTypeInContext(Context);
                else if (_fx.BitCount == 64)
                    return DoubleTypeInContext(Context);
                else if (_fx.BitCount == 80)
                    return X86FP80TypeInContext(Context);
                else if (_fx.BitCount == 128)
                    return FP128TypeInContext(Context);
                else
                {
                    Console.WriteLine($"internal compiler error: unsupported float type f{_fx.BitCount}");
                    Environment.Exit(1);
                    return default;
                }
            }
#endif

            case SymbolType.String: return StructTypeInContext(Context, new LLVMTypeRef[] { GetLlvmType(SymbolTypes.UInt), PointerType(Int8TypeInContext(Context), 0) }, false);

            case SymbolType.RawPtr: return PointerType(Int8TypeInContext(Context), 0);
            case SymbolType.Array array: return ArrayType(GetLlvmType(array.ElementType), array.ElementCount);
            case SymbolType.Pointer pointer: return PointerType(GetLlvmType(pointer.ElementType), 0);
            case SymbolType.Buffer buffer: return PointerType(GetLlvmType(buffer.ElementType), 0);
            case SymbolType.Slice slice:
            {
                var dataPointerType = PointerType(GetLlvmType(slice.ElementType), 0);
                return StructTypeInContext(Context, new LLVMTypeRef[] { GetLlvmType(SymbolTypes.UInt), dataPointerType }, false);
            }

            case SymbolType.Function function:
            {
                var llvmReturnType = GetLlvmType(function.ReturnType);

                var llvmParameterTypes = new LLVMTypeRef[function.Parameters.Length];
                for (int i = 0; i < llvmParameterTypes.Length; i++)
                    llvmParameterTypes[i] = GetLlvmType(function.Parameters[i].Type);

                bool isCVarArgs = function.VarArgs == VarArgsKind.C;
                return FunctionType(llvmReturnType, llvmParameterTypes, isCVarArgs);
            }

            case SymbolType.FunctionPointer functionPointer:
            {
                var llvmReturnType = GetLlvmType(functionPointer.ReturnType);

                var llvmParameterTypes = new LLVMTypeRef[functionPointer.ParameterTypes.Length];
                for (int i = 0; i < llvmParameterTypes.Length; i++)
                    llvmParameterTypes[i] = GetLlvmType(functionPointer.ParameterTypes[i]);

                bool isCVarArgs = functionPointer.VarArgs == VarArgsKind.C;
                return PointerType(FunctionType(llvmReturnType, llvmParameterTypes, isCVarArgs), 0);
            }

            case SymbolType.Struct _struct:
            {
                var elementTypes = new LLVMTypeRef[_struct.Fields.Length];
                for (int i = 0; i < elementTypes.Length; i++)
                {
                    var field = _struct.Fields[i];
                    elementTypes[i] = GetLlvmType(field.Type);
                }

                return StructTypeInContext(Context, elementTypes, false);
            }

            default:
            {
                Console.WriteLine($"internal compiler error: invalid Laye type {type.GetType().Name} in LLVM backend");
                Environment.Exit(1);
                return default;
            }
        }
    }

    private LLVMValueRef DeclareFunction(LayeCst.FunctionDeclaration function)
    {
        var llvmFunctionType = GetLlvmType(function.FunctionSymbol.Type!);
        var llvmFunctionValue = AddFunction(Module, function.FunctionSymbol.Name, llvmFunctionType);

        if (function.Modifiers.ExternLibrary is not null && function.Modifiers.ExternLibrary != "C")
            m_externLibraryReferences.Add(function.Modifiers.ExternLibrary);

        LLVMCallConv llvmCallConv;
        switch (function.FunctionSymbol.Type!.CallingConvention)
        {
            case CallingConvention.Laye: llvmCallConv = LLVMCallConv.LLVMCCallConv; break;
            case CallingConvention.LayeNoContext: llvmCallConv = LLVMCallConv.LLVMCCallConv; break;
            case CallingConvention.CDecl: llvmCallConv = LLVMCallConv.LLVMCCallConv; break;
            case CallingConvention.StdCall: llvmCallConv = LLVMCallConv.LLVMX86StdcallCallConv; break;
            case CallingConvention.FastCall: llvmCallConv = LLVMCallConv.LLVMX86FastcallCallConv; break;

            default:
            {
                Console.WriteLine($"internal compiler error: invalid calling convention value {function.FunctionSymbol.Type!.CallingConvention}");
                Environment.Exit(1);
                return default;
            }
        }

        SetFunctionCallConv(llvmFunctionValue, (uint)llvmCallConv);

        return llvmFunctionValue;
    }

    private void CompileFunction(LayeCst.FunctionDeclaration function)
    {
        var functionValue = m_functions[function.FunctionSymbol];
        var builder = new LlvmFunctionBuilder(this, functionValue, CreateBuilderInContext(Context));

        var entryBlock = builder.AppendBlock(".entry");
        builder.PositionAtEnd(entryBlock);

        for (int i = 0; i < function.ParameterSymbols.Length; i++)
        {
            var param = function.ParameterSymbols[i];

            var paramAddress = builder.BuildAlloca(param.Type!, param.Name);
            BuildStore(builder.Builder, GetParam(functionValue, (uint)i), paramAddress.Value);

            builder.SetSymbolAddress(param, paramAddress);
        }

        switch (function.Body)
        {
            case LayeCst.BlockFunctionBody block: CompileBlock(builder, block.BodyBlock); break;

            default:
            {
                Console.WriteLine($"internal compiler error: unhandled function block in LLVM backend {function.Body.GetType().Name}");
                Environment.Exit(1);
                return;
            }
        }

        if (function.FunctionSymbol.Type!.ReturnType is SymbolType.Void)
        {
            foreach (var block in builder.BasicBlocks)
            {
                var lastInsn = GetLastInstruction(block);
                if (IsABranchInst(lastInsn).Pointer.ToInt64() == 0 && IsAReturnInst(lastInsn).Pointer.ToInt64() == 0)
                    builder.BuildReturnVoid();
            }
        }
    }

    private void CompileBlock(LlvmFunctionBuilder builder, LayeCst.Block block)
    {
        foreach (var statement in block.Body)
            CompileStatement(builder, statement);
    }

    private void CompileStatement(LlvmFunctionBuilder builder, LayeCst.Stmt statement)
    {
        switch (statement)
        {
            case LayeCst.DeadCode: break; // pass on dead code that we left in the cst

            case LayeCst.Block block: CompileBlock(builder, block); break;

            case LayeCst.If ifStmt:
            {
                var passBlock = builder.AppendBlock("if.pass");
                var failBlock = builder.AppendBlock("if.fail");
                var continueBlock = builder.AppendBlock("if.continue");

                var condition = CompileExpression(builder, ifStmt.Condition);
                builder.BuildConditionalBranch(condition, passBlock, failBlock);

                builder.PositionAtEnd(passBlock);
                {
                    CompileStatement(builder, ifStmt.IfBody);
                    builder.BuildBranch(continueBlock);
                }

                builder.PositionAtEnd(failBlock);
                {
                    if (ifStmt.ElseBody is not null)
                        CompileStatement(builder, ifStmt.ElseBody);
                    builder.BuildBranch(continueBlock);
                }

                builder.PositionAtEnd(continueBlock);
            } break;

            case LayeCst.While whileStmt:
            {
                var recheckBlock = builder.AppendBlock("while.check");
                var passBlock = builder.AppendBlock("while.pass");
                var failBlock = builder.AppendBlock("while.fail");
                var continueBlock = builder.AppendBlock("while.continue");

                var firstCondition = CompileExpression(builder, whileStmt.Condition);
                builder.BuildConditionalBranch(firstCondition, passBlock, failBlock);

                builder.PositionAtEnd(recheckBlock);
                {
                    var condition = CompileExpression(builder, whileStmt.Condition);
                    builder.BuildConditionalBranch(condition, passBlock, continueBlock);
                }

                builder.PositionAtEnd(passBlock);
                {
                    CompileStatement(builder, whileStmt.WhileBody);
                    builder.BuildBranch(recheckBlock);
                }

                builder.PositionAtEnd(failBlock);
                {
                    if (whileStmt.ElseBody is not null)
                        CompileStatement(builder, whileStmt.ElseBody);
                    builder.BuildBranch(continueBlock);
                }

                builder.PositionAtEnd(continueBlock);
            } break;

            case LayeCst.BindingDeclaration bindingDecl:
            {
                var bindingAddress = builder.BuildAlloca(bindingDecl.BindingSymbol.Type!, bindingDecl.BindingName.Image);
                builder.SetSymbolAddress(bindingDecl.BindingSymbol, bindingAddress);

                if (bindingDecl.Expression is LayeCst.Expr expression)
                {
                    var expressionValue = CompileExpression(builder, expression);
                    builder.BuildStore(expressionValue, bindingAddress);
                }
            } break;

            case LayeCst.Return returnStmt:
            {
                if (returnStmt.ReturnValue is null)
                    builder.BuildReturnVoid();
                else
                {
                    var returnValue = CompileExpression(builder, returnStmt.ReturnValue);
                    builder.BuildReturn(returnValue);
                }
            } break;

            case LayeCst.ExpressionStatement exprStmt: CompileExpression(builder, exprStmt.Expression); break;

            case LayeCst.Assignment assignmentStmt:
            {
                var target = CompileExpressionAsLValue(builder, assignmentStmt.TargetExpression);
                var value = CompileExpression(builder, assignmentStmt.ValueExpression);
                builder.BuildStore(value, target);
            } break;

            default:
            {
                Console.WriteLine($"internal compiler error: unhandled statement in LLVM backend {statement.GetType().Name}");
                Environment.Exit(1);
                return;
            }
        }
    }

    private LlvmValue<SymbolType.Pointer> CompileExpressionAsLValue(LlvmFunctionBuilder builder, LayeCst.Expr expression)
    {
        switch (expression)
        {
            case LayeCst.LoadValue load: return builder.GetSymbolAddress(load.Symbol);

            case LayeCst.NamedIndex namedIndex:
            {
                var target = CompileExpressionAsLValue(builder, namedIndex.TargetExpression);
                return builder.BuildGetStructFieldAddress(target, namedIndex.Name.Image);
            }

            case LayeCst.DynamicIndex dynamicIndex:
            {
                var target = CompileExpressionAsLValue(builder, dynamicIndex.TargetExpression);
                var indices = dynamicIndex.Arguments.Select(arg => CompileExpression(builder, arg)).ToArray();

                switch (target.Type.ElementType)
                {
                    case SymbolType.Buffer bufferType:
                    {
                        Debug.Assert(indices.Length == 1);
                        return builder.BuildGetBufferIndexAddress(target, indices[0]);
                    }

                    case SymbolType.Slice sliceType:
                    {
                        Debug.Assert(indices.Length == 1);
                        return builder.BuildGetSliceIndexAddress(target, indices[0]);
                    }

                    default:
                    {
                        Console.WriteLine($"internal compiler error: unhandled dynamic index target type in LLVM backend ({target.Type})");
                        Environment.Exit(1);
                        return default!;
                    }
                }
            }

            default:
            {
                Console.WriteLine($"internal compiler error: unhandled expression in LLVM backend ({expression.GetType().Name})");
                Environment.Exit(1);
                return default!;
            }
        }
    }

    private TypedLlvmValue CompileExpression(LlvmFunctionBuilder builder, LayeCst.Expr expression)
    {
        if (expression.CheckIsLValue())
        {
            var lvalue = CompileExpressionAsLValue(builder, expression);
            return builder.BuildLoad(lvalue);
        }

        switch (expression)
        {
            case LayeCst.String _string:
            {
                TypedLlvmValue stringValue;
                if (_string.Type == SymbolTypes.ReadOnlyU8Buffer)
                    stringValue = builder.BuildGlobalStringBuffer(_string.Literal.LiteralValue);
                else if (_string.Type is SymbolType.String)
                    stringValue = builder.BuildGlobalString(_string.Literal.LiteralValue);
                else
                {
                    Console.WriteLine($"internal compiler error: unhandled or invalid string type in LLVM backend ({_string.Type})");
                    Environment.Exit(1);
                    return default;
                }

                return stringValue;
            }

            case LayeCst.Integer _int:
            {
                TypedLlvmValue intValue;
                intValue = builder.ConstInteger(_int.Type, _int.Literal.LiteralValue);
                return intValue;
            }

            case LayeCst.Float _float:
            {
                TypedLlvmValue floatValue;
                floatValue = builder.ConstFloat(_float.Type, _float.Literal.LiteralValue);
                return floatValue;
            }

            case LayeCst.StringLengthLookup stringLengthLookup:
            {
                // TODO(local): probably wrap this up into builder.BuildLoadStringLengthFromValue
                var stringValue = CompileExpression(builder, stringLengthLookup.TargetExpression);
                var stringStorageAddress = builder.BuildAlloca(SymbolTypes.String, "string_tempstorage");
                builder.BuildStore(stringValue, stringStorageAddress);
                return builder.BuildLoadStringLengthFromAddress(stringStorageAddress);
            }

            case LayeCst.StringDataLookup stringDataLookup:
            {
                // TODO(local): probably wrap this up into builder.BuildLoadStringLengthFromValue
                var stringValue = CompileExpression(builder, stringDataLookup.TargetExpression);
                var stringStorageAddress = builder.BuildAlloca(SymbolTypes.String, "string_tempstorage");
                builder.BuildStore(stringValue, stringStorageAddress);
                return builder.BuildLoadStringDataFromAddress(stringStorageAddress);
            }

            case LayeCst.Slice slice:
            {
                var targetValue = CompileExpression(builder, slice.TargetExpression);

                var offsetValue = slice.OffsetExpression is null ? null : CompileExpression(builder, slice.OffsetExpression);
                var countValue = slice.CountExpression is null ? null : CompileExpression(builder, slice.CountExpression);

                switch (slice.TargetExpression.Type)
                {
                    case SymbolType.Buffer:
                    {
                        Debug.Assert(countValue is not null);
                        return builder.BuildSliceFromBuffer(targetValue, offsetValue, countValue!);
                    }

                    default:
                    {
                        Console.WriteLine($"internal compiler error: unhandled slice target in LLVM backend ({slice.TargetExpression.Type})");
                        Environment.Exit(1);
                        return default!;
                    }
                }
            }

            case LayeCst.SliceToString sliceToString:
            {
                var targetValue = CompileExpression(builder, sliceToString.SliceExpression);
                return builder.BuildSliceToString(targetValue);
            }

            case LayeCst.Negate negate:
            {
                var value = CompileExpression(builder, negate.Expression);
                return builder.BuildNegate(value);
            }

            case LayeCst.AddressOf addr: return CompileExpressionAsLValue(builder, addr.Expression);
            case LayeCst.ValueAt valueAt:
            {
                var valueAddress = CompileExpression(builder, valueAt.Expression);
                return builder.BuildLoad(valueAddress);
            }

            case LayeCst.LogicalNot not:
            {
                var value = CompileExpression(builder, not.Expression);
                return builder.BuildNot(value);
            }

            case LayeCst.Add add:
            {
                var left = CompileExpression(builder, add.LeftExpression);
                var right = CompileExpression(builder, add.RightExpression);
                return builder.BuildAdd(left, right);
            }

            case LayeCst.Subtract subtract:
            {
                var left = CompileExpression(builder, subtract.LeftExpression);
                var right = CompileExpression(builder, subtract.RightExpression);
                return builder.BuildSubtract(left, right);
            }

            case LayeCst.Multiply multiply:
            {
                var left = CompileExpression(builder, multiply.LeftExpression);
                var right = CompileExpression(builder, multiply.RightExpression);
                return builder.BuildMultiply(left, right);
            }

            case LayeCst.Divide divide:
            {
                var left = CompileExpression(builder, divide.LeftExpression);
                var right = CompileExpression(builder, divide.RightExpression);
                return builder.BuildDivide(left, right);
            }

            case LayeCst.Remainder remainder:
            {
                var left = CompileExpression(builder, remainder.LeftExpression);
                var right = CompileExpression(builder, remainder.RightExpression);
                return builder.BuildRemainder(left, right);
            }

            case LayeCst.CompareEqual equal:
            {
                var left = CompileExpression(builder, equal.LeftExpression);
                var right = CompileExpression(builder, equal.RightExpression);
                return builder.BuildEqual(left, right);
            }

            case LayeCst.CompareNotEqual notEqual:
            {
                var left = CompileExpression(builder, notEqual.LeftExpression);
                var right = CompileExpression(builder, notEqual.RightExpression);
                return builder.BuildNotEqual(left, right);
            }

            case LayeCst.CompareLess less:
            {
                var left = CompileExpression(builder, less.LeftExpression);
                var right = CompileExpression(builder, less.RightExpression);
                return builder.BuildLess(left, right);
            }

            case LayeCst.CompareLessEqual lessEqual:
            {
                var left = CompileExpression(builder, lessEqual.LeftExpression);
                var right = CompileExpression(builder, lessEqual.RightExpression);
                return builder.BuildLessEqual(left, right);
            }

            case LayeCst.CompareGreater greater:
            {
                var left = CompileExpression(builder, greater.LeftExpression);
                var right = CompileExpression(builder, greater.RightExpression);
                return builder.BuildGreater(left, right);
            }

            case LayeCst.CompareGreaterEqual greaterEqual:
            {
                var left = CompileExpression(builder, greaterEqual.LeftExpression);
                var right = CompileExpression(builder, greaterEqual.RightExpression);
                return builder.BuildGreaterEqual(left, right);
            }

            case LayeCst.LeftShift lsh:
            {
                var left = CompileExpression(builder, lsh.LeftExpression);
                var right = CompileExpression(builder, lsh.RightExpression);
                return builder.BuildLeftShift(left, right);
            }

            case LayeCst.RightShift rsh:
            {
                var left = CompileExpression(builder, rsh.LeftExpression);
                var right = CompileExpression(builder, rsh.RightExpression);
                return builder.BuildRightShift(left, right);
            }

            case LayeCst.BitwiseAnd bitand:
            {
                var left = CompileExpression(builder, bitand.LeftExpression);
                var right = CompileExpression(builder, bitand.RightExpression);
                return builder.BuildBitAnd(left, right);
            }

            case LayeCst.BitwiseOr bitor:
            {
                var left = CompileExpression(builder, bitor.LeftExpression);
                var right = CompileExpression(builder, bitor.RightExpression);
                return builder.BuildBitOr(left, right);
            }

            case LayeCst.BitwiseXor bitxor:
            {
                var left = CompileExpression(builder, bitxor.LeftExpression);
                var right = CompileExpression(builder, bitxor.RightExpression);
                return builder.BuildBitXor(left, right);
            }

            case LayeCst.BitwiseComplement bitcompl:
            {
                var value = CompileExpression(builder, bitcompl.Expression);
                return builder.BuildBitComplemet(value);
            }

            case LayeCst.LogicalAnd logicaland:
            {
                var resultStorage = builder.BuildAlloca(SymbolTypes.Bool, "logand.result");

                var firstTestBlock = builder.AppendBlock("logand.left");
                var secondTestBlock = builder.AppendBlock("logand.right");
                var continueBlock = builder.AppendBlock("logand.continue");

                builder.BuildBranch(firstTestBlock);

                builder.PositionAtEnd(firstTestBlock);
                {
                    var firstTest = CompileExpression(builder, logicaland.LeftExpression);
                    builder.BuildStore(firstTest, resultStorage);

                    builder.BuildConditionalBranch(builder.BuildLoad(resultStorage), secondTestBlock, continueBlock);
                }

                builder.PositionAtEnd(secondTestBlock);
                {
                    var secondTest = CompileExpression(builder, logicaland.RightExpression);
                    builder.BuildStore(secondTest, resultStorage);

                    builder.BuildBranch(continueBlock);
                }

                builder.PositionAtEnd(continueBlock);
                return builder.BuildLoad(resultStorage);
            }

            case LayeCst.LogicalOr logicalor:
            {
                var resultStorage = builder.BuildAlloca(SymbolTypes.Bool, "logor.result");

                var firstTestBlock = builder.AppendBlock("logor.left");
                var secondTestBlock = builder.AppendBlock("logor.right");
                var continueBlock = builder.AppendBlock("logor.continue");

                builder.BuildBranch(firstTestBlock);

                builder.PositionAtEnd(firstTestBlock);
                {
                    var firstTest = CompileExpression(builder, logicalor.LeftExpression);
                    builder.BuildStore(firstTest, resultStorage);

                    builder.BuildConditionalBranch(builder.BuildLoad(resultStorage), continueBlock, secondTestBlock);
                }

                builder.PositionAtEnd(secondTestBlock);
                {
                    var secondTest = CompileExpression(builder, logicalor.RightExpression);
                    builder.BuildStore(secondTest, resultStorage);

                    builder.BuildBranch(continueBlock);
                }

                builder.PositionAtEnd(continueBlock);
                return builder.BuildLoad(resultStorage);
            }

            case LayeCst.TypeCast typeCast:
            {
                var targetValue = CompileExpression(builder, typeCast.Expression);
                switch (typeCast.Type)
                {
                    case SymbolType.RawPtr:
                    {
                        if (targetValue.Type is SymbolType.Integer || targetValue.Type is SymbolType.SizedInteger)
                            return builder.BuildIntToRawPtrCast(targetValue);
                        else if (targetValue.Type is SymbolType.Buffer)
                            return builder.BuildAnyPointerToRawPtrCast(targetValue);
                    } break;

                    case SymbolType.Pointer pointerType:
                    {
                        if (targetValue.Type is SymbolType.Integer || targetValue.Type is SymbolType.SizedInteger)
                            return builder.BuildIntToPointerCast(targetValue, pointerType);
                        else if (targetValue.Type is SymbolType.Pointer valuePointerType)
                        {
                            if (valuePointerType.ElementType == pointerType.ElementType)
                                return new LlvmValue<SymbolType.Pointer>(targetValue.Value, pointerType);
                        }
                    } break;

                    case SymbolType.Buffer bufferType:
                    {
                        if (targetValue.Type is SymbolType.RawPtr)
                            return builder.BuildRawPtrToAnyPointerCast(targetValue, bufferType);
                        else if (targetValue.Type is SymbolType.Integer || targetValue.Type is SymbolType.SizedInteger)
                            return builder.BuildIntToBufferCast(targetValue, bufferType);
                        else if (targetValue.Type is SymbolType.Buffer valueBufferType)
                        {
                            if (valueBufferType.ElementType == bufferType.ElementType)
                                return new LlvmValue<SymbolType.Buffer>(targetValue.Value, bufferType);
                        }
                    } break;

                    case SymbolType.Integer:
                    case SymbolType.SizedInteger:
                    {
                        if (targetValue.Type.IsNumeric())
                            return new TypedLlvmValue(BuildIntCast(builder.Builder, targetValue.Value, GetLlvmType(typeCast.Type), ""), typeCast.Type);
                    } break;
                }

                Console.WriteLine($"internal compiler error: unhandled type cast in LLVM backend ({typeCast.Expression.Type} to {typeCast.Type})");
                Environment.Exit(1);
                return default!;
            }

            case LayeCst.InvokeFunction invokeFunction:
            {
                var args = new TypedLlvmValue[invokeFunction.Arguments.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = CompileExpression(builder, invokeFunction.Arguments[i]);

                return builder.BuildCall(invokeFunction.TargetFunctionSymbol, args);
            }

            default:
            {
                Console.WriteLine($"internal compiler error: unhandled expression in LLVM backend ({expression.GetType().Name})");
                Environment.Exit(1);
                return default!;
            }
        }
    }
}

internal sealed class LlvmFunctionBuilder
{
    public LlvmBackend Backend { get; }
    public LLVMValueRef FunctionValue { get; }
    public LLVMBuilderRef Builder { get; }

    public LLVMContextRef Context => Backend.Context;

    private readonly List<LLVMBasicBlockRef> m_blocks = new();
    private int m_currentBlockIndex = -1;

    public IEnumerable<LLVMBasicBlockRef> BasicBlocks => m_blocks;

    private readonly Dictionary<Symbol, LlvmValue<SymbolType.Pointer>> m_valueAddresses = new();

    public LlvmFunctionBuilder(LlvmBackend backend, LLVMValueRef functionValue, LLVMBuilderRef builder)
    {
        Backend = backend;
        FunctionValue = functionValue;
        Builder = builder;
    }

    private void CheckCanBuild()
    {
        Debug.Assert(m_currentBlockIndex >= 0, "cannot build because we are not positioned within any blocks");
    }

    public LLVMBasicBlockRef AppendBlock(string blockName = "block")
    {
        var block = AppendBasicBlock(FunctionValue, blockName);
        m_blocks.Add(block);
        return block;
    }

    public void PositionAtEnd(LLVMBasicBlockRef block)
    {
        Debug.Assert(m_blocks.Contains(block), "cannot position builder at end of a block that is not in this function.");

        PositionBuilderAtEnd(Builder, block);
        m_currentBlockIndex = m_blocks.IndexOf(block);
    }

    public void SetSymbolAddress(Symbol symbol, LlvmValue<SymbolType.Pointer> llvmValue) => m_valueAddresses[symbol] = llvmValue;
    public LlvmValue<SymbolType.Pointer> GetSymbolAddress(Symbol symbol) => m_valueAddresses[symbol];

    public LlvmValue<TIntType> ConstInteger<TIntType>(TIntType intType, ulong constantValue)
        where TIntType : SymbolType
    {
        bool signed = intType is SymbolType.Integer _int ? _int.Signed :
            (intType is SymbolType.SizedInteger _ix ? _ix.Signed :
            throw new ArgumentException($"type {intType} is not integer", nameof(intType)));

        var constIntValue = ConstInt(Backend.GetLlvmType(intType), constantValue, signed);
        return new LlvmValue<TIntType>(constIntValue, intType);
    }

    public LlvmValue<TFloatType> ConstFloat<TFloatType>(TFloatType floatType, double constantValue)
        where TFloatType : SymbolType
    {
        if (floatType is not SymbolType.Float /* && floatType is not SymbolType.SizedFloat */)
            throw new ArgumentException("type is not float", nameof(floatType));

        var constFloatValue = ConstReal(Backend.GetLlvmType(floatType), constantValue);
        return new LlvmValue<TFloatType>(constFloatValue, floatType);
    }

    public LlvmValue<SymbolType.Buffer> BuildGlobalStringBuffer(string value, string name = "global_string_buffer")
    {
        CheckCanBuild();
        var globalStringPtr = BuildGlobalStringPtr(Builder, value, name);
        return new LlvmValue<SymbolType.Buffer>(globalStringPtr, SymbolTypes.ReadOnlyU8Buffer);
    }

    public LlvmValue<SymbolType.String> BuildGlobalString(string value, string name = "global_string")
    {
        CheckCanBuild();
        ulong stringLength = (ulong)System.Text.Encoding.UTF8.GetByteCount(value);

        var stringStorageAddress = BuildAlloca(SymbolTypes.String);

        var stringLengthAddress = BuildStructGEP(Builder, stringStorageAddress.Value, 0, "global_string.value.length");
        var stringLengthValue = ConstInt(Backend.GetLlvmType(SymbolTypes.UInt), stringLength, false);
        LLVM.BuildStore(Builder, stringLengthValue, stringLengthAddress);

        var stringPointerAddress = BuildStructGEP(Builder, stringStorageAddress.Value, 1, "global_string.value.pointer");
        var globalStringPtr = BuildGlobalStringPtr(Builder, value, name);
        LLVM.BuildStore(Builder, globalStringPtr, stringPointerAddress);

        var stringStorageValue = LLVM.BuildLoad(Builder, stringStorageAddress.Value, "global_string.value");
        return new LlvmValue<SymbolType.String>(stringStorageValue, SymbolTypes.String);
    }

#if false
    public LlvmValue<SymbolType.Slice> BuildGlobalStringSlice(string value, string name = "global_string_slice")
    {
        var globalStringPtr = BuildGlobalStringPtr(Builder, value, name);
        var stringAddressValue = new LlvmValue<SymbolType.Pointer>(globalStringPtr, m_u8Ptr);

        var sliceAddressValue = BuildAlloca(m_u8SlcReadOnly);

        uint byteCount = (uint)Encoding.UTF8.GetByteCount(value);
        var count = ConstInteger(m_uint, byteCount);

        BuildSetSliceFields(builder, sliceAddress, stringAddress, count);

        return new(LLVM.BuildLoad(builder, sliceAddress, DefaultInstructionName), m_u8SlcReadOnly);
    }
#endif

#if false
    public LlvmStructValue BuildGlobalStringStruct(LlvmFunctionBuilder builder, string stringValue)
    {
        var stringAddress = BuildAlloca(builder, builder.Backend.StringType.LayeType);
        var stringSliceValueAddress = LLVM.BuildStructGEP(builder, stringAddress, 0, "");
        LLVM.BuildStore(builder, BuildGlobalStringSlice(builder, stringValue), stringSliceValueAddress);
        return new LlvmStructValue(LLVM.BuildLoad(builder, stringAddress, ""), (LayeStructTypeSymbol)builder.Backend.StringType.LayeType);
    }
#endif

    public LlvmValue<SymbolType.Pointer> BuildAlloca(SymbolType type, string name = "")
    {
        CheckCanBuild();
        var llvmType = Backend.GetLlvmType(type);
        var allocaAddressResult = LLVM.BuildAlloca(Builder, llvmType, name);
        return new(allocaAddressResult, new SymbolType.Pointer(type));
    }

    public TypedLlvmValue BuildLoad(LlvmValue<SymbolType.Pointer> address, string name = "")
    {
        CheckCanBuild();
        var loadValue = LLVM.BuildLoad(Builder, address.Value, name);
        return new TypedLlvmValue(loadValue, address.Type.ElementType);
    }

    public TypedLlvmValue BuildLoad(TypedLlvmValue address, string name = "")
    {
        CheckCanBuild();
        Debug.Assert(address.Type is SymbolType.Pointer);
        var pointerType = (SymbolType.Pointer)address.Type;
        var loadValue = LLVM.BuildLoad(Builder, address.Value, name);
        return new TypedLlvmValue(loadValue, pointerType.ElementType);
    }

    public void BuildStore(TypedLlvmValue value, LlvmValue<SymbolType.Pointer> address)
    {
        CheckCanBuild();
        Debug.Assert(value.Type == address.Type.ElementType, $"type checker did not ensure value and address types were the same ({value.Type} != {address.Type.ElementType})");
        LLVM.BuildStore(Builder, value.Value, address.Value);
    }

    public LlvmValue<SymbolType.Pointer> BuildGetStructFieldAddress(TypedLlvmValue structTarget, string fieldName)
    {
        CheckCanBuild();
        Debug.Assert(structTarget.Type is SymbolType.Pointer targetAddr && targetAddr.ElementType is SymbolType.Struct);

        var structType = (SymbolType.Struct)((SymbolType.Pointer)structTarget.Type).ElementType;
        var structFields = structType.Fields;

        uint fieldIndex;
        for (fieldIndex = 0; fieldIndex < structFields.Length; fieldIndex++)
        {
            if (structFields[fieldIndex].Name == fieldName)
                break;
        }

        var field = structFields[fieldIndex];

        var address = BuildStructGEP(Builder, structTarget.Value, fieldIndex, "struct_field_addr");
        return new(address, new SymbolType.Pointer(field.Type));
    }

    public LlvmValue<SymbolType.Pointer> BuildGetBufferIndexAddress(TypedLlvmValue bufferTarget, TypedLlvmValue index)
    {
        CheckCanBuild();
        Debug.Assert(bufferTarget.Type is SymbolType.Pointer targetAddr && targetAddr.ElementType is SymbolType.Buffer);
        Debug.Assert(index.Type is SymbolType.Integer);

        var bufferType = (SymbolType.Buffer)((SymbolType.Pointer)bufferTarget.Type).ElementType;
        var bufferElementType = bufferType.ElementType;

        var bufferValue = LLVM.BuildLoad(Builder, bufferTarget.Value, "buffer_value");
        var address = BuildInBoundsGEP(Builder, bufferValue, new[] { index.Value }, "buffer_index_addr");
        return new(address, new SymbolType.Pointer(bufferElementType));
    }

    public LlvmValue<SymbolType.Pointer> BuildGetSliceIndexAddress(TypedLlvmValue sliceTarget, TypedLlvmValue index)
    {
        CheckCanBuild();
        Debug.Assert(sliceTarget.Type is SymbolType.Pointer targetAddr && targetAddr.ElementType is SymbolType.Slice);
        Debug.Assert(index.Type == SymbolTypes.UInt);

        var sliceType = (SymbolType.Slice)((SymbolType.Pointer)sliceTarget.Type).ElementType;
        var sliceElementType = sliceType.ElementType;

        //var sliceValue = LLVM.BuildLoad(Builder, sliceTarget.Value, "slice_value");
        var sliceDataValue = LLVM.BuildLoad(Builder, BuildStructGEP(Builder, sliceTarget.Value, 1, "slice_data_value_addr"), "slice_data_value");
        var address = BuildInBoundsGEP(Builder, sliceDataValue, new[] { index.Value }, "slice_data_index_addr");
        return new(address, new SymbolType.Pointer(sliceElementType));
    }

    public LlvmValue<SymbolType.Integer> BuildLoadStringLengthFromAddress(LlvmValue<SymbolType.Pointer> stringAddress)
    {
        CheckCanBuild();
        var lengthAddress = BuildStructGEP(Builder, stringAddress.Value, 0, "string.length.addr");
        return new(LLVM.BuildLoad(Builder, lengthAddress, "string.length"), SymbolTypes.UInt);
    }

    public LlvmValue<SymbolType.Buffer> BuildLoadStringDataFromAddress(LlvmValue<SymbolType.Pointer> stringAddress)
    {
        CheckCanBuild();
        var dataAddress = BuildStructGEP(Builder, stringAddress.Value, 1, "string.data.addr");
        return new(LLVM.BuildLoad(Builder, dataAddress, "string.data"), SymbolTypes.ReadOnlyU8Buffer);
    }

    public LlvmValue<SymbolType.Slice> BuildSliceFromBuffer(TypedLlvmValue bufferValue, TypedLlvmValue? offset, TypedLlvmValue count)
    {
        CheckCanBuild();
        Debug.Assert(bufferValue.Type is SymbolType.Buffer);
        if (offset is not null) Debug.Assert(offset.Type.IsInteger());
        Debug.Assert(count.Type.IsInteger());

        if (count.Type != SymbolTypes.UInt)
            count = new(BuildIntCast(Builder, count.Value, Backend.GetLlvmType(SymbolTypes.UInt), ""), SymbolTypes.UInt);

        var sliceType = new SymbolType.Slice(((SymbolType.Buffer)bufferValue.Type).ElementType);

        var sliceStorageAddress = BuildAlloca(sliceType, "slice_value");

        var sliceLengthAddress = BuildStructGEP(Builder, sliceStorageAddress.Value, 0, "slice_value.length");
        LLVM.BuildStore(Builder, count.Value, sliceLengthAddress);

        var slicePointerAddress = BuildStructGEP(Builder, sliceStorageAddress.Value, 1, "slice_value.pointer");
        if (offset is not null)
        {
            if (offset.Type != SymbolTypes.UInt)
                offset = new(BuildIntCast(Builder, offset.Value, Backend.GetLlvmType(SymbolTypes.UInt), ""), SymbolTypes.UInt);

            var bufferAsInt = BuildIntToPtr(Builder, bufferValue.Value, Backend.GetLlvmType(SymbolTypes.UInt), "");
            var bufferPlusOffset = LLVM.BuildAdd(Builder, bufferAsInt, offset.Value, "");
            var bufferAsPtr = BuildPtrToInt(Builder, bufferPlusOffset, Backend.GetLlvmType(bufferValue.Type), "");
            LLVM.BuildStore(Builder, bufferAsPtr, slicePointerAddress);
        }
        else LLVM.BuildStore(Builder, bufferValue.Value, slicePointerAddress);

        var stringStorageValue = LLVM.BuildLoad(Builder, sliceStorageAddress.Value, "slice_value.value");
        return new LlvmValue<SymbolType.Slice>(stringStorageValue, sliceType);
    }

    public LlvmValue<SymbolType.String> BuildSliceToString(TypedLlvmValue sliceValue)
    {
        CheckCanBuild();
        Debug.Assert(sliceValue.Type is SymbolType.Slice);

        var sliceStorageAddress = BuildAlloca(sliceValue.Type);
        var stringStorageAddress = BuildAlloca(SymbolTypes.String);

        LLVM.BuildStore(Builder, sliceValue.Value, sliceStorageAddress.Value);

        var sliceLengthAddress = BuildStructGEP(Builder, sliceStorageAddress.Value, 0, "slice_to_string.slice_length_pointer");
        var sliceLengthValue = LLVM.BuildLoad(Builder, sliceLengthAddress, "slice_to_string.slice_length");

        var sliceValueAddress = BuildStructGEP(Builder, sliceStorageAddress.Value, 1, "slice_to_string.slice_value_pointer");
        var sliceValueValue = LLVM.BuildLoad(Builder, sliceValueAddress, "slice_to_string.slice_value");

        var stringLengthAddress = BuildStructGEP(Builder, stringStorageAddress.Value, 0, "slice_to_string.value.length");
        LLVM.BuildStore(Builder, sliceLengthValue, stringLengthAddress);

        var stringPointerAddress = BuildStructGEP(Builder, stringStorageAddress.Value, 1, "slice_to_string.value.pointer");
        var stringPointerValue = BuildMallocRaw(sliceLengthValue);
        BuildMemCpyRaw(stringPointerValue, sliceValueValue, sliceLengthValue);
        LLVM.BuildStore(Builder, stringPointerValue, stringPointerAddress);

        var stringStorageValue = LLVM.BuildLoad(Builder, stringStorageAddress.Value, "slice_to_string.value");
        return new LlvmValue<SymbolType.String>(stringStorageValue, SymbolTypes.String);
    }

    private LLVMValueRef BuildMallocRaw(LLVMValueRef count)
    {
        CheckCanBuild();
        //LLVM.GetNamedFunction(Backend.Module, "default_allocator");
        var mallocFunction = GetNamedFunction(Backend.Module, "malloc");
        return LLVM.BuildCall(Builder, mallocFunction, new[] { count }, "malloc.result");
    }

    private LLVMValueRef BuildMemCpyRaw(LLVMValueRef dest, LLVMValueRef src, LLVMValueRef count)
    {
        CheckCanBuild();
        var memcpyFunction = GetNamedFunction(Backend.Module, "memcpy");
        return LLVM.BuildCall(Builder, memcpyFunction, new[] { dest, src, count }, "memcpy.result");
    }

    public TypedLlvmValue BuildNegate(TypedLlvmValue left)
    {
        CheckCanBuild();
        if (left.Type.IsInteger())
            return new(BuildNeg(Builder, left.Value, ""), left.Type);
        else return new(BuildFNeg(Builder, left.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildNot(TypedLlvmValue left)
    {
        CheckCanBuild();
        return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntEQ, left.Value, ConstInt(Int1TypeInContext(Context), 0, false), ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildAdd(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(LLVM.BuildAdd(Builder, left.Value, right.Value, ""), left.Type);
        else return new(BuildFAdd(Builder, left.Value, right.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildSubtract(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildSub(Builder, left.Value, right.Value, ""), left.Type);
        else return new(BuildFSub(Builder, left.Value, right.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildMultiply(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildMul(Builder, left.Value, right.Value, ""), left.Type);
        else return new(BuildFMul(Builder, left.Value, right.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildDivide(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildSDiv(Builder, left.Value, right.Value, ""), left.Type);
            else return new(BuildUDiv(Builder, left.Value, right.Value, ""), left.Type);
        }
        else return new(BuildFDiv(Builder, left.Value, right.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildRemainder(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildSRem(Builder, left.Value, right.Value, ""), left.Type);
            else return new(BuildURem(Builder, left.Value, right.Value, ""), left.Type);
        }
        else return new(BuildFRem(Builder, left.Value, right.Value, ""), left.Type);
    }

    public TypedLlvmValue BuildEqual(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntEQ, left.Value, right.Value, ""), SymbolTypes.Bool);
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealOEQ, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildNotEqual(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntNE, left.Value, right.Value, ""), SymbolTypes.Bool);
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealONE, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildLess(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntSLT, left.Value, right.Value, ""), SymbolTypes.Bool);
            else return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntULT, left.Value, right.Value, ""), SymbolTypes.Bool);
        }
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealOLT, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildLessEqual(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntSLE, left.Value, right.Value, ""), SymbolTypes.Bool);
            else return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntULE, left.Value, right.Value, ""), SymbolTypes.Bool);
        }
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealOLE, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildGreater(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntSGT, left.Value, right.Value, ""), SymbolTypes.Bool);
            else return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntUGT, left.Value, right.Value, ""), SymbolTypes.Bool);
        }
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealOGT, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildGreaterEqual(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntSGE, left.Value, right.Value, ""), SymbolTypes.Bool);
            else return new(BuildICmp(Builder, LLVMIntPredicate.LLVMIntUGE, left.Value, right.Value, ""), SymbolTypes.Bool);
        }
        else return new(BuildFCmp(Builder, LLVMRealPredicate.LLVMRealOGE, left.Value, right.Value, ""), SymbolTypes.Bool);
    }

    public TypedLlvmValue BuildLeftShift(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildShl(Builder, left.Value, right.Value, ""), left.Type);
        else
        {
            Console.WriteLine($"internal compiler error: left shift with non-integer types ({left.Type}, {right.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public TypedLlvmValue BuildRightShift(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
        {
            if (left.Type.IsSigned())
                return new(BuildAShr(Builder, left.Value, right.Value, ""), left.Type);
            else return new(BuildLShr(Builder, left.Value, right.Value, ""), left.Type);
        }
        else
        {
            Console.WriteLine($"internal compiler error: right shift with non-integer types ({left.Type}, {right.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public TypedLlvmValue BuildBitAnd(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildAnd(Builder, left.Value, right.Value, ""), left.Type);
        else
        {
            Console.WriteLine($"internal compiler error: bit and with non-integer types ({left.Type}, {right.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public TypedLlvmValue BuildBitOr(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildOr(Builder, left.Value, right.Value, ""), left.Type);
        else
        {
            Console.WriteLine($"internal compiler error: bit or with non-integer types ({left.Type}, {right.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public TypedLlvmValue BuildBitXor(TypedLlvmValue left, TypedLlvmValue right)
    {
        CheckCanBuild();
        Debug.Assert(left.Type == right.Type);
        if (left.Type.IsInteger())
            return new(BuildXor(Builder, left.Value, right.Value, ""), left.Type);
        else
        {
            Console.WriteLine($"internal compiler error: bit xor with non-integer types ({left.Type}, {right.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public TypedLlvmValue BuildBitComplemet(TypedLlvmValue value)
    {
        CheckCanBuild();
        if (value.Type.IsInteger())
            return new(LLVM.BuildNot(Builder, value.Value, ""), value.Type);
        else
        {
            Console.WriteLine($"internal compiler error: bit complement with non-integer type ({value.Type}) in LLVM backend");
            Environment.Exit(1);
            return default!;
        }
    }

    public void BuildBranch(LLVMBasicBlockRef block)
    {
        CheckCanBuild();
        Debug.Assert(m_blocks.Contains(block), "block to branch to does not exist within this function");

        BuildBr(Builder, block);
    }

    public void BuildConditionalBranch(TypedLlvmValue condition, LLVMBasicBlockRef pass, LLVMBasicBlockRef fail)
    {
        CheckCanBuild();
        Debug.Assert(m_blocks.Contains(pass), "block to branch to does not exist within this function");
        Debug.Assert(m_blocks.Contains(fail), "block to branch to does not exist within this function");

        BuildCondBr(Builder, condition.Value, pass, fail);
    }

    public void BuildReturn(TypedLlvmValue value) => BuildRet(Builder, value.Value);
    public void BuildReturnVoid() => BuildRetVoid(Builder);

    public TypedLlvmValue BuildCall(Symbol.Function functionSymbol, TypedLlvmValue[] arguments)
    {
        var functionValue = Backend.GetFunctionValueFromSymbol(functionSymbol);
        var functionResult = LLVM.BuildCall(Builder, functionValue, arguments.Select(a => a.Value).ToArray(), "");

        if (functionSymbol.Type!.ReturnType is SymbolType.Void)
            return new LlvmValueVoid();

        return new TypedLlvmValue(functionResult, functionSymbol.Type!.ReturnType);
    }

    public TypedLlvmValue BuildCall(LlvmValue<SymbolType.FunctionPointer> functionPointer, TypedLlvmValue[] arguments)
    {
        var functionValue = LLVM.BuildLoad(Builder, functionPointer.Value, "load.fnptr");
        var functionResult = LLVM.BuildCall(Builder, functionValue, arguments.Select(a => a.Value).ToArray(), "");

        if (functionPointer.Type!.ReturnType is SymbolType.Void)
            return new LlvmValueVoid();

        return new TypedLlvmValue(functionResult, functionPointer.Type!.ReturnType);
    }

    public LlvmValue<SymbolType.Pointer> BuildIntToPointerCast(TypedLlvmValue value, SymbolType.Pointer pointerType)
    {
        var cast = BuildIntToPtr(Builder, value.Value, Backend.GetLlvmType(pointerType), "int.to.pointer");
        return new(cast, pointerType);
    }

    public LlvmValue<SymbolType.Buffer> BuildIntToBufferCast(TypedLlvmValue value, SymbolType.Buffer bufferType)
    {
        var cast = BuildIntToPtr(Builder, value.Value, Backend.GetLlvmType(bufferType), "int.to.buffer");
        return new(cast, bufferType);
    }

    public LlvmValue<SymbolType.RawPtr> BuildIntToRawPtrCast(TypedLlvmValue value)
    {
        var cast = BuildIntToPtr(Builder, value.Value, PointerType(Int8TypeInContext(Context), 0), "int.to.rawptr");
        return new(cast, new());
    }

    public LlvmValue<SymbolType.RawPtr> BuildAnyPointerToRawPtrCast(TypedLlvmValue value)
    {
        var cast = BuildPointerCast(Builder, value.Value, PointerType(Int8TypeInContext(Context), 0), "anyptr.to.rawptr");
        return new(cast, new());
    }

    public TypedLlvmValue BuildRawPtrToAnyPointerCast(TypedLlvmValue value, SymbolType type)
    {
        var cast = BuildPointerCast(Builder, value.Value, Backend.GetLlvmType(type), "rawptr.to.anyptr");
        return new(cast, type);
    }
}

internal record class TypedLlvmValue(LLVMValueRef Value, SymbolType Type);
internal record class LlvmValueVoid() : TypedLlvmValue(default, new SymbolType.Void());
internal record class LlvmValue<TType>(LLVMValueRef Value, TType HardType) : TypedLlvmValue(Value, HardType)
    where TType : SymbolType
{
    public new TType Type => (TType)base.Type;
}
