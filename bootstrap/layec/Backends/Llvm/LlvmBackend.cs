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
    public readonly SymbolType.Slice ReadOnlyU8SlcType = new(new SymbolType.SizedInteger(false, 8), true);

    private readonly Dictionary<Symbol.Function, LLVMValueRef> m_functions = new();

    public void Compile(LayeIrModule[] modules, BackendOptions options)
    {
        string lmoduleName = Path.GetFileNameWithoutExtension(options.OutputFileName);

        Context = ContextCreate();
        Module = ModuleCreateWithNameInContext(lmoduleName, Context);

        foreach (var module in modules)
        {
            foreach (var function in module.Functions)
            {
                m_functions[function.Symbol] = DeclareFunction(function);
            }
        }

        foreach (var module in modules)
        {
            foreach (var function in module.Functions)
            {
                if (function.BasicBlocks.Length > 0)
                    CompileFunction(function);
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

        // i686-pc-windows-gnu
        string additionalFiles = string.Join(" ", options.FilesToLinkAgainst.Select(f => $"\"{f}\""));
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

        if (!options.ShowClangOutput)
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
                else throw new NotImplementedException();
            }

            case SymbolType.RawPtr: return PointerType(VoidTypeInContext(Context), 0);
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

            default: throw new NotImplementedException();
        }
    }

    private LLVMValueRef DeclareFunction(LayeIr.Function function)
    {
        var llvmFunctionType = GetLlvmType(function.Symbol.Type!);
        var llvmFunctionValue = AddFunction(Module, function.Name.Image, llvmFunctionType);
        
        SetFunctionCallConv(llvmFunctionValue, (uint)(function.Symbol.Type!.CallingConvention switch
        {
            CallingConvention.Laye => LLVMCallConv.LLVMCCallConv,
            CallingConvention.LayeNoContext => LLVMCallConv.LLVMCCallConv,

            CallingConvention.CDecl => LLVMCallConv.LLVMCCallConv,
            CallingConvention.StdCall => LLVMCallConv.LLVMX86StdcallCallConv,
            CallingConvention.FastCall => LLVMCallConv.LLVMX86FastcallCallConv,

            _ => throw new NotImplementedException(),
        }));

        return llvmFunctionValue;
    }

    private void CompileFunction(LayeIr.Function function)
    {
        var functionValue = m_functions[function.Symbol];
        var builder = new LlvmFunctionBuilder(this, functionValue, CreateBuilderInContext(Context));

        var entryBlock = builder.AppendBlock(".entry");

        var blockValues = new LLVMBasicBlockRef[function.BasicBlocks.Length];
        for (int i = 0; i < blockValues.Length; i++)
        {
            var llvmBlock = builder.AppendBlock();
            blockValues[i] = llvmBlock;
        }

        builder.PositionAtEnd(entryBlock);
        builder.BuildBranch(blockValues[0]);

        for (int i = 0; i < blockValues.Length; i++)
        {
            var layeBlock = function.BasicBlocks[i];
            var llvmBlock = blockValues[i];

            builder.PositionAtEnd(llvmBlock);

            foreach (var insn in layeBlock.Instructions)
            {
                switch (insn)
                {
                    case LayeIr.InvokeGlobalFunction _invokeGlobal:
                    {
                        var callResult = builder.BuildCall(_invokeGlobal.GlobalFunction, _invokeGlobal.Arguments);
                        if (_invokeGlobal.GlobalFunction.Type!.ReturnType is not SymbolType.Void)
                            builder.SetValue(_invokeGlobal, callResult);
                    } break;

                    case LayeIr.Value value: CompileValue(builder, value); break;

                    default: throw new NotImplementedException();
                }
            }

            switch (layeBlock.BranchInstruction)
            {
                case LayeIr.Return _return: builder.BuildReturn(_return.ReturnValue); break;
                case LayeIr.ReturnVoid: builder.BuildReturnVoid(); break;
                default: throw new NotImplementedException();
            }
        }
    }

    private TypedLlvmValue CompileValue(LlvmFunctionBuilder builder, LayeIr.Value value)
    {
        switch (value)
        {
            case LayeIr.String _string:
            {
                TypedLlvmValue stringValue;
                stringValue = builder.BuildGlobalStringPointer(_string.LiteralValue);
                builder.SetValue(_string, stringValue);
                return stringValue;
            } break;

            // TODO(local): the integer constants should have types associated with them
            case LayeIr.Integer _int:
            {
                var intType = _int.Type;
                intType = UInt64Type;

                TypedLlvmValue intValue;
                intValue = builder.ConstInteger(intType, _int.LiteralValue);
                builder.SetValue(_int, intValue);
                return intValue;
            } break;

            // TODO(local): the integer constants should have types associated with them
            case LayeIr.IntToRawPtrCast _rawptrCast:
            {
                var toCastValue = CompileValue(builder, _rawptrCast.CastValue);
                var castValue = builder.BuildIntToRawPtrCast(toCastValue);
                builder.SetValue(_rawptrCast, castValue);
                return castValue;
            } break;

            case LayeIr.InvokeGlobalFunction _invokeGlobal:
            {
                var callResult = builder.BuildCall(_invokeGlobal.GlobalFunction, _invokeGlobal.Arguments);
                if (_invokeGlobal.GlobalFunction.Type!.ReturnType is not SymbolType.Void)
                    builder.SetValue(_invokeGlobal, callResult);
                return callResult;
            } break;

            default: throw new NotImplementedException();
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

    private readonly Dictionary<LayeIr.Value, TypedLlvmValue> m_values = new();

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

    public void SetValue(LayeIr.Value irValue, TypedLlvmValue llvmValue) => m_values[irValue] = llvmValue;

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

    public void BuildBranch(LLVMBasicBlockRef block)
    {
        CheckCanBuild();
        Debug.Assert(m_blocks.Contains(block), "block to branch to does not exist within this function");

        BuildBr(Builder, block);
    }

    public void BuildReturn(LayeIr.Value value)
    {
        Debug.Assert(m_values.ContainsKey(value), "cannot return a value which has not been created yet");
        BuildRet(Builder, m_values[value].Value);
    }

    public void BuildReturnVoid()
    {
        BuildRetVoid(Builder);
    }

    public TypedLlvmValue BuildCall(Symbol.Function functionSymbol, LayeIr.Value[] arguments)
    {
        var functionValue = Backend.GetFunctionValueFromSymbol(functionSymbol);
        var functionResult = LLVM.BuildCall(Builder, functionValue, arguments.Select(a => m_values[a].Value).ToArray(), "");

        if (functionSymbol.Type!.ReturnType is SymbolType.Void)
            return new LlvmValueVoid();

        return new TypedLlvmValue(functionResult, functionSymbol.Type!.ReturnType);
    }

    public LlvmValue<SymbolType.RawPtr> BuildIntToRawPtrCast(TypedLlvmValue value)
    {
        var cast = BuildIntToPtr(Builder, value.Value, PointerType(VoidTypeInContext(Context), 0), "int.to.rawptr");
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
