using System.Diagnostics;

using LLVMSharp;
using static LLVMSharp.LLVM;

namespace laye.Backends.Llvm;

internal sealed class LlvmBackend : IBackend
{
    public LLVMContextRef Context;
    public LLVMModuleRef Module;

    public readonly SymbolType.Integer IntType = new(false);
    public readonly SymbolType.Integer UIntType = new(true);

    public readonly SymbolType.SizedInteger Int32Type = new(true, 32);
    public readonly SymbolType.SizedInteger Int64Type = new(true, 64);
    public readonly SymbolType.SizedInteger UInt64Type = new(false, 64);

    public readonly SymbolType.Pointer U8PtrType = new(new SymbolType.SizedInteger(false, 8));
    public readonly SymbolType.Slice U8SlcType = new(new SymbolType.SizedInteger(false, 8));
    public readonly SymbolType.Slice ReadOnlyU8SlcType = new(new SymbolType.SizedInteger(false, 8), AccessKind.ReadOnly);

    private readonly Dictionary<Symbol.Function, LLVMValueRef> m_functions = new();

    private readonly HashSet<string> m_externLibraryReferences = new();

    public void Compile(LayeCstRoot[] roots, BackendOptions options)
    {
        string lmoduleName = Path.GetFileNameWithoutExtension(options.OutputFileName);

        Context = ContextCreate();
        Module = ModuleCreateWithNameInContext(lmoduleName, Context);

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

        //if (moduleParams.Kind == ModuleKind.Console || moduleParams.Kind == ModuleKind.Windows)
        //Debug.Assert(asmDef.EntryPoint is not null, "no program entry point specified for executable, program checker failed");

        string llFileName = Path.ChangeExtension(options.OutputFileName, ".ll");
        string bcFileName = Path.ChangeExtension(options.OutputFileName, ".bc");

        LLVMBool printResult = PrintModuleToFile(Module, llFileName, out string printError);
        if (printResult.Value != 0)
        {
            Console.WriteLine(printError);
            Environment.Exit(1);
            return;
        }

        int bcWriteResult = WriteBitcodeToFile(Module, bcFileName);
        if (bcWriteResult != 0)
        {
            Console.WriteLine("Failed to generate LLVM bitcode file.");
            Environment.Exit(1);
            return;
        }

        string outFileName = options.OutputFileName;
        //outFileName = Path.ChangeExtension(outFileName, ".o");

        IEnumerable<string> filesToLinkAgainst = options.FilesToLinkAgainst.Concat(m_externLibraryReferences.Select(ex => $"-l{ex}"));

        // i686-pc-windows-gnu
        string additionalFiles = string.Join(" ", filesToLinkAgainst.Select(f => $"\"{f}\""));
        string clangArguments = $"-target {options.TargetTriple} \"{bcFileName}\" {additionalFiles} -g -v \"-o{outFileName}\"";
        if (options.IsExecutable)
            ;// clangArguments += " -Wl,-ENTRY:_laye_start";
        else clangArguments += " -shared -fuse-ld=llvm-lib";

        if (options.AdditionalArguments.Length > 0)
        {
            string additionalArguments = string.Join(" ", options.AdditionalArguments.Select(a => $"\"{a}\""));
            clangArguments += $" {additionalArguments}";
        }

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

        int exitCode = clangProcess.ExitCode;
        Environment.Exit(exitCode);
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

            case SymbolType.Bool: return GetLlvmType(UIntType);
            case SymbolType.SizedBool _bx: return IntTypeInContext(Context, _bx.BitCount);

            case SymbolType.Integer: return Int64TypeInContext(Context);
            case SymbolType.SizedInteger _ix: return IntTypeInContext(Context, _ix.BitCount);

            case SymbolType.Float: return DoubleTypeInContext(Context);
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

            case SymbolType.RawPtr: return PointerType(Int8TypeInContext(Context), 0);
            case SymbolType.Array array: return ArrayType(GetLlvmType(array.ElementType), array.ElementCount);
            case SymbolType.Pointer pointer: return PointerType(GetLlvmType(pointer.ElementType), 0);
            case SymbolType.Buffer buffer: return PointerType(GetLlvmType(buffer.ElementType), 0);
            case SymbolType.Slice slice:
            {
                var dataPointerType = PointerType(GetLlvmType(slice.ElementType), 0);
                return StructTypeInContext(Context, new LLVMTypeRef[] { GetLlvmType(UIntType), dataPointerType }, false);
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

        builder.BuildReturnVoid();
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

            case LayeCst.ExpressionStatement exprStmt: CompileExpression(builder, exprStmt.Expression); break;

            default:
            {
                Console.WriteLine($"internal compiler error: unhandled statement in LLVM backend {statement.GetType().Name}");
                Environment.Exit(1);
                return;
            }
        }
    }

    private TypedLlvmValue CompileExpression(LlvmFunctionBuilder builder, LayeCst.Expr expression)
    {
        switch (expression)
        {
            case LayeCst.String _string:
            {
                TypedLlvmValue stringValue;
                stringValue = builder.BuildGlobalStringPointer(_string.Literal.LiteralValue);
                return stringValue;
            }

            case LayeCst.Integer _int:
            {
                TypedLlvmValue intValue;
                intValue = builder.ConstInteger(_int.Type, _int.Literal.LiteralValue);
                return intValue;
            }

            case LayeCst.LoadValue load:
            {
                var address = builder.GetSymbolAddress(load.Symbol);
                return builder.BuildLoad(address, load.Symbol.Name);
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
                        if (targetValue.Type is SymbolType.Integer || targetValue.Type is SymbolType.SizedInteger)
                            return builder.BuildIntToBufferCast(targetValue, bufferType);
                        else if (targetValue.Type is SymbolType.Buffer valueBufferType)
                        {
                            if (valueBufferType.ElementType == bufferType.ElementType)
                                return new LlvmValue<SymbolType.Buffer>(targetValue.Value, bufferType);
                        }
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
            throw new ArgumentException("type is not integer", nameof(intType)));

        var constIntValue = ConstInt(Backend.GetLlvmType(intType), constantValue, signed);
        return new LlvmValue<TIntType>(constIntValue, intType);
    }

    public LlvmValue<TFloatType> ConstFloat<TFloatType>(TFloatType floatType, double constantValue)
        where TFloatType : SymbolType
    {
        if (floatType is not SymbolType.Float && floatType is not SymbolType.SizedFloat)
            throw new ArgumentException("type is not float", nameof(floatType));

        var constFloatValue = ConstReal(Backend.GetLlvmType(floatType), constantValue);
        return new LlvmValue<TFloatType>(constFloatValue, floatType);
    }

    public LlvmValue<SymbolType.Pointer> BuildGlobalStringPointer(string value, string name = "global_string_pointer")
    {
        var globalStringPtr = BuildGlobalStringPtr(Builder, value, name);
        return new LlvmValue<SymbolType.Pointer>(globalStringPtr, Backend.U8PtrType);
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
        var llvmType = Backend.GetLlvmType(type);
        var allocaAddressResult = LLVM.BuildAlloca(Builder, llvmType, name);
        return new(allocaAddressResult, new SymbolType.Pointer(type));
    }

    public TypedLlvmValue BuildLoad(LlvmValue<SymbolType.Pointer> address, string name = "")
    {
        var loadValue = LLVM.BuildLoad(Builder, address.Value, name);
        return new TypedLlvmValue(loadValue, address.Type.ElementType);
    }

    public void BuildStore(TypedLlvmValue value, LlvmValue<SymbolType.Pointer> address)
    {
        Debug.Assert(value.Type == address.Type.ElementType, "type checker did not ensure value and address types were the same");
        LLVM.BuildStore(Builder, value.Value, address.Value);
    }

    public void BuildBranch(LLVMBasicBlockRef block)
    {
        CheckCanBuild();
        Debug.Assert(m_blocks.Contains(block), "block to branch to does not exist within this function");

        BuildBr(Builder, block);
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
}

internal record class TypedLlvmValue(LLVMValueRef Value, SymbolType Type);
internal record class LlvmValueVoid() : TypedLlvmValue(default, new SymbolType.Void());
internal record class LlvmValue<TType>(LLVMValueRef Value, TType HardType) : TypedLlvmValue(Value, HardType)
    where TType : SymbolType
{
    public new TType Type => (TType)base.Type;
}
