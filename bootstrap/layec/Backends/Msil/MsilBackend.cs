using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;
using Mono.Collections;
using Mono.Collections.Generic;
using Mono.CompilerServices;
using Mono.CompilerServices.SymbolWriter;

namespace laye.Backends.Msil;

internal sealed class MsilBackend : IBackend
{
    private AssemblyDefinition m_asmDef = default!;
    private ModuleDefinition MainModule => m_asmDef.MainModule;

    private TypeDefinition m_globalType = default!;

    private readonly Dictionary<Symbol, MethodDefinition> m_functionMethods = new();
    //private readonly Dictionary<Type, TypeReference> m_importTypes = new();

    private ModuleReference m_crtDef = default!;

    public void Compile(LayeIrModule[] modules, BackendOptions options)
    {
        string moduleName = Path.GetFileNameWithoutExtension(options.OutputFileName);
        var asmNameDef = new AssemblyNameDefinition(moduleName, options.Version);
        var moduleParams = new ModuleParameters()
        {
            Architecture = TargetArchitecture.I386,
            //Runtime = TargetRuntime.Net_4_0,
            Kind = ModuleKind.Console,
            //AssemblyResolver = new DefaultAssemblyResolver(),
        };

        var asmDef = AssemblyDefinition.CreateAssembly(asmNameDef, moduleName, moduleParams);

        /*
        void ImportType(Type type) => m_importTypes[type] = asmDef.MainModule.ImportReference(type);
        void Import<T>() => ImportType(typeof(T));

        ImportType(typeof(void));
        Import<object>();
        Import<sbyte>();
        Import<byte>();
        Import<short>();
        Import<ushort>();
        Import<int>();
        Import<uint>();
        Import<long>();
        Import<ulong>();
        Import<float>();
        Import<double>();
        */

        var globalType = new TypeDefinition("", "__laye_Global", TypeAttributes.Class | TypeAttributes.Sealed, asmDef.MainModule.TypeSystem.Object);
        asmDef.MainModule.Types.Add(globalType);

        m_crtDef = new ModuleReference("msvcrt.dll");
        asmDef.MainModule.ModuleReferences.Add(m_crtDef);

        m_asmDef = asmDef;
        m_globalType = globalType;

        // TODO(local): compile

        foreach (var module in modules)
        {
            foreach (var function in module.Functions)
            {
                DeclareFunction(function);
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

        if (moduleParams.Kind == ModuleKind.Console || moduleParams.Kind == ModuleKind.Windows)
            Debug.Assert(asmDef.EntryPoint is not null, "no program entry point specified for executable, program checker failed");

        asmDef.Write(options.OutputFileName);
    }

    private TypeReference ResolveDotNetType(SymbolType type)
    {
        switch (type)
        {
            case SymbolType.Void: return m_asmDef.MainModule.TypeSystem.Void;
            case SymbolType.Integer _int: return _int.Signed ? m_asmDef.MainModule.TypeSystem.Int64 : m_asmDef.MainModule.TypeSystem.UInt64;
            case SymbolType.SizedInteger _ix:
            {
                switch (_ix.BitCount)
                {
                    case 8 : return _ix.Signed ? m_asmDef.MainModule.TypeSystem.SByte : m_asmDef.MainModule.TypeSystem.Byte;
                    case 16: return _ix.Signed ? m_asmDef.MainModule.TypeSystem.Int16 : m_asmDef.MainModule.TypeSystem.UInt16;
                    case 32: return _ix.Signed ? m_asmDef.MainModule.TypeSystem.Int32 : m_asmDef.MainModule.TypeSystem.UInt32;
                    case 64: return _ix.Signed ? m_asmDef.MainModule.TypeSystem.Int64 : m_asmDef.MainModule.TypeSystem.UInt64;
                    default: throw new NotImplementedException();
                }
            }
            case SymbolType.Float: return m_asmDef.MainModule.TypeSystem.Double;
            case SymbolType.Buffer buffer: return new PointerType(ResolveDotNetType(buffer.ElementType));
            default: throw new NotImplementedException();
        }
    }

    private void DeclareFunction(LayeIr.Function function)
    {
        var returnTypeRef = ResolveDotNetType(function.Symbol.Type!.ReturnType);
        var functionDef = new MethodDefinition(function.Name.Image, MethodAttributes.Static | MethodAttributes.Public, returnTypeRef);
        m_functionMethods[function.Symbol] = functionDef;

        foreach (var param in function.Symbol.Type!.Parameters)
        {
            var paramTypeRef = ResolveDotNetType(param.Type);
            functionDef.Parameters.Add(new ParameterDefinition(param.Name, ParameterAttributes.None, paramTypeRef));
        }

#if false
        if (function.Symbol.Type.VarArgs == VarArgsKind.C)
        {
            var varargsType = new TypeReference("System", nameof(RuntimeArgumentHandle), null, null, true);
            functionDef.Parameters.Add(new ParameterDefinition(varargsType));
        }
#endif

        if (function.BasicBlocks.Length == 0)
        {
            functionDef.IsPreserveSig = true;
            //functionDef.IsUnmanaged = true;
            //functionDef.CustomAttributes.Add(new CustomAttribute())
            functionDef.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvCdecl, function.Name.Image, m_crtDef);
        }
        else
        {
            functionDef.IsIL = true;
            if (function.Name.Image == "main")
                m_asmDef.EntryPoint = functionDef;
        }

        m_globalType.Methods.Add(functionDef);
    }

    private void CompileFunction(LayeIr.Function function)
    {
        var method = m_functionMethods[function.Symbol];

        var methodBody = method.Body;
        var builder = methodBody.GetILProcessor();

        var blockLabels = new Dictionary<LayeIr.BasicBlock, Instruction>();
        foreach (var block in function.BasicBlocks)
        {
            var instruction = Instruction.Create(OpCodes.Nop);
            builder.Append(instruction);

            blockLabels[block] = instruction;
        }

        foreach (var block in function.BasicBlocks)
        {
            var blockLabel = blockLabels[block];
            var lastInstruction = blockLabel;

            foreach (var insn in block.Instructions)
            {
                switch (insn)
                {
                    case LayeIr.InvokeGlobalFunction invokeInsn:
                    {
                        var targetFunctionSymbol = invokeInsn.GlobalFunction;
                        foreach (var arg in invokeInsn.Arguments)
                            PushValue(builder, ref lastInstruction, arg);

                        var callInsn = Instruction.Create(OpCodes.Call, m_functionMethods[targetFunctionSymbol]);
                        builder.InsertAfter(lastInstruction, callInsn);
                        lastInstruction = callInsn;
                    } break;

                    case LayeIr.Value _value: break;

                    default: throw new NotImplementedException();
                }
            }

            switch (block.BranchInstruction)
            {
                case LayeIr.ReturnVoid: builder.InsertAfter(lastInstruction, Instruction.Create(OpCodes.Ret)); break;

                default: throw new NotImplementedException();
            }
        }

        methodBody.Optimize();
    }

    private void PushValue(ILProcessor builder, ref Instruction insertAfter, LayeIr.Value expr)
    {
        switch (expr)
        {
            case LayeIr.String stringLiteral:
            {
#if false
                //Encoding.UTF8.GetBytes()
                var ldstr = Instruction.Create(OpCodes.Ldstr, stringLiteral.LiteralValue);
                builder.InsertAfter(insertAfter, ldstr);
                insertAfter = ldstr;

                var temp = m_asmDef.MainModule.TypeSystem.Object;
                var encodingTypeRef = new TypeReference("System.Text", "Encoding", temp.Module, temp.Scope);
                var ldsfld = Instruction.Create(OpCodes.Ldsfld, new FieldReference("UTF8", encodingTypeRef));
                builder.InsertAfter(insertAfter, ldsfld);
                insertAfter = ldsfld;

                var GetBytesRef = new MethodReference("GetBytes", encodingTypeRef);
                GetBytesRef.Parameters.Add(new ParameterDefinition(m_asmDef.MainModule.TypeSystem.String));
                GetBytesRef.ReturnType = m_asmDef.MainModule.TypeSystem.Byte.MakeArrayType();
                var callvirt = Instruction.Create(OpCodes.Callvirt, GetBytesRef);
                builder.InsertAfter(insertAfter, callvirt);
                insertAfter = callvirt;
#else
                byte[] bytes = Encoding.UTF8.GetBytes(stringLiteral.LiteralValue);
                bytes = bytes.Concat(Enumerable.Repeat((byte)0, 1)).ToArray();
                var literalResource = new EmbeddedResource("stringResource", ManifestResourceAttributes.Private, bytes);
                m_asmDef.MainModule.Resources.Add(literalResource);
#endif
            } break;

            case LayeIr.Integer intLiteral:
            {
                var ldstr = Instruction.Create(OpCodes.Ldc_I8, (long)intLiteral.LiteralValue);
                builder.InsertAfter(insertAfter, ldstr);
                insertAfter = ldstr;
            } break;

            default: throw new NotImplementedException();
        }
    }
}
