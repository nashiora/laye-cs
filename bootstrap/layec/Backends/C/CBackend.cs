using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static CompilerStatus;

namespace laye.Backends.C;

internal sealed class CBackend : IBackend
{
    private static readonly HashSet<string> ReservedNames = new()
    {
        "main", "_start",
        // stdio
        "fclose", "clearerr", "feof", "ferror", "fflush", "fgetpos", "fopen", "fread", "freopen", "fseek", "fsetpos", "ftell", "fwrite", "remove", "rename", "rewind", "setbuf", "setvbuf", "tmpfile", "tmpnam", "fprintf", "printf", "sprintf", "vfprintf", "vprintf", "vsprintf", "fscanf", "scanf", "sscanf", "fgetc", "fgets", "fputc", "fputs", "getc", "getchar", "gets", "putc", "putchar", "puts", "ungetc", "perror",
        "SEEK_SET", "SEEK_END",
        // stdlib
        "abort", "calloc", "exit", "free", "malloc", "realloc",
        // string
        "memcpy", "memset", "strlen",
    };

    private readonly HashSet<string> m_externLibraryReferences = new();

    private readonly StringBuilder m_includes = new();
    private readonly StringBuilder m_defines = new();
    private readonly StringBuilder m_typedefs = new();
    private readonly StringBuilder m_typeDecls = new();
    private readonly StringBuilder m_prototypes = new();
    private readonly StringBuilder m_internalFunctionDecls = new();
    private readonly StringBuilder m_functionDecls = new();

    //private readonly List<string> m_typeDecls = new();

    private readonly Dictionary<SymbolType, string> m_cTypeNames = new();
    private readonly Dictionary<Symbol, string> m_symbolNames = new();
    private readonly Dictionary<SymbolType, string> m_sliceOperationFunctionNames = new();

    private int m_uniqueIndexCounter = 1;

    private int m_tabs = 0;

    public int Compile(LayeCstRoot[] roots, BackendOptions options)
    {
        var irCompilerTimer = Stopwatch.StartNew();

        GenerateCType(SymbolTypes.String);
        GenerateCType(SymbolTypes.U8Slice);

        string u8SliceTypeName = m_cTypeNames[SymbolTypes.U8Slice];

        m_includes.AppendLine("#include <stddef.h>");
        m_includes.AppendLine("#include <stdint.h>");
        m_includes.AppendLine("#include <stdbool.h>");

        m_defines.AppendLine("#define LYSTR_STRING(lit, len) ((ly_string){ .data = (uint8_t*)(lit), .length = len })");
        m_defines.AppendLine($"#define LYSTR_SLICE(lit, len) (({u8SliceTypeName}){{ .data = (uint8_t*)(lit), .length = len }})");

        m_includes.AppendLine("#include <stdio.h>");
        m_includes.AppendLine("#include <stdlib.h>");
        m_includes.AppendLine("#include <string.h>");

        m_typedefs.AppendLine("typedef int8_t ly_bool8_t;");
        m_typedefs.AppendLine("typedef int16_t ly_bool16_t;");
        m_typedefs.AppendLine("typedef int32_t ly_bool32_t;");
        m_typedefs.AppendLine("typedef int64_t ly_bool64_t;");
        m_typedefs.AppendLine("typedef intptr_t ly_int_t;");
        m_typedefs.AppendLine("typedef uintptr_t ly_uint_t;");
        m_typedefs.AppendLine("typedef struct { void* data; ly_uint_t count; ly_uint_t capacity; } ly_dynamic_t;");

        m_prototypes.AppendLine("ly_string ly_internal_substring(ly_string s_, ly_uint_t o_, ly_uint_t l_);");
        m_prototypes.AppendLine($"ly_string ly_internal_slicetostring({u8SliceTypeName} s_);");

        m_internalFunctionDecls.AppendLine(@"ly_string ly_internal_substring(ly_string s_, ly_uint_t o_, ly_uint_t l_)
{
    ly_string result;
    result.data = s_.data + o_;
    result.length = l_;
    return result;
}");

        m_internalFunctionDecls.AppendLine(@$"ly_string ly_internal_slicetostring({u8SliceTypeName} s_)
{{
    ly_string result;
    result.data = malloc(s_.length);
    memcpy(result.data, s_.data, s_.length);
    result.length = s_.length;
    return result;
}}");

        m_internalFunctionDecls.AppendLine(@$"void ly_internal_dynamic_ensure_capacity(ly_dynamic_t* d_, ly_uint_t reqiredCapacity)
{{
	ly_uint_t capacity = d_->capacity;
	if (capacity < reqiredCapacity)
	{{
        ly_uint_t desiredCapacity = capacity == 0 ? reqiredCapacity * 2 : capacity * 2;
        d_->capacity = desiredCapacity;
		d_->data = realloc(d_->data, desiredCapacity);
	}}
}}");

        foreach (var root in roots)
        {
            foreach (var node in root.TopLevelNodes)
            {
                switch (node)
                {
                    case LayeCst.FunctionDeclaration fnDecl:
                        string name = fnDecl.FunctionSymbol.Name;
                        if (ReservedNames.Contains(name) && fnDecl.Body is not LayeCst.EmptyFunctionBody)
                            name = $"ly_creservedfn_{name}";

                        m_symbolNames[fnDecl.FunctionSymbol] = name;
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
                    case LayeCst.FunctionDeclaration fnDecl: CompileFunction(fnDecl); break;
                }
            }
        }

        irCompilerTimer.Stop();

        var irCompilerElapsedTime = irCompilerTimer.Elapsed;
        ShowInfo($"Compiled to C in {irCompilerElapsedTime.TotalSeconds:N2}s");

        string cFileName = Path.ChangeExtension(options.OutputFileName, ".c");
        if (File.Exists(cFileName)) File.Delete(cFileName);

        using (var writer = new StreamWriter(File.OpenWrite(cFileName)))
        {
            writer.WriteLine("// includes");
            writer.WriteLine(m_includes.ToString());
            writer.WriteLine("// defines");
            writer.WriteLine(m_defines.ToString());
            writer.WriteLine("// typedefs");
            writer.WriteLine(m_typedefs.ToString());
            writer.WriteLine("// type declarations");
            writer.WriteLine(m_typeDecls.ToString());
            writer.WriteLine("// function prototypes");
            writer.WriteLine(m_prototypes.ToString());
            writer.WriteLine("// functions");
            writer.WriteLine(m_internalFunctionDecls.ToString());
            writer.WriteLine(m_functionDecls.ToString());
            writer.WriteLine("// entry point");

            string EntryFunction = $@"
int main(int argc, char** argv) {{
  ly_creservedfn_main((int32_t)argc, (uint8_t**)argv);
  return 0;
}}";

            writer.WriteLine(EntryFunction);
        }

        string outFileName = options.OutputFileName;
        string llFileName = Path.ChangeExtension(options.OutputFileName, ".ll");

        var outFileWriterTimer = Stopwatch.StartNew();

        IEnumerable<string> filesToLinkAgainst = options.FilesToLinkAgainst.Concat(m_externLibraryReferences.Select(ex => $"-l{ex}"));
        // i686-pc-windows-gnu
        string additionalFiles = string.Join(" ", filesToLinkAgainst.Select(f => $"\"{f}\""));

        {
            string clangArguments = $"-S -emit-llvm \"{cFileName}\" {additionalFiles} -g -v \"-o{llFileName}\"";
            if (!string.IsNullOrWhiteSpace(options.TargetTriple))
                clangArguments += $" -target {options.TargetTriple}";
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
                if (File.Exists(cFileName)) File.Delete(cFileName);
                if (File.Exists(llFileName)) File.Delete(llFileName);
                if (File.Exists(ilkFileName)) File.Delete(ilkFileName);
                if (File.Exists(pdbFileName)) File.Delete(pdbFileName);
            }

            outFileWriterTimer.Stop();

            if (clangProcess.ExitCode == 0)
            {
                var outFileWriterElapsedTime = outFileWriterTimer.Elapsed;
                ShowInfo($"Generated LLVM IR file in {outFileWriterElapsedTime.TotalSeconds:N2}s");
            }
            else ShowInfo($"Failed to generate LLVM IR file (clang exited with code {clangProcess.ExitCode})");

            if (clangProcess.ExitCode != 0)
                return clangProcess.ExitCode;
        }

        {
            string clangArguments = $"\"{cFileName}\" {additionalFiles} -g -v \"-o{outFileName}\"";
            if (!string.IsNullOrWhiteSpace(options.TargetTriple))
                clangArguments += $" -target {options.TargetTriple}";
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
                if (File.Exists(cFileName)) File.Delete(cFileName);
                if (File.Exists(llFileName)) File.Delete(llFileName);
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
    }

    private void AppendTabs(StringBuilder builder)
    {
        for (int i = 0; i < m_tabs; i++)
            builder.Append("  ");
    }

    private void AppendCType(StringBuilder builder, SymbolType type)
    {
        switch (type)
        {
            case SymbolType.Void: builder.Append("void"); break;
            case SymbolType.Bool: builder.Append("ly_bool8_t"); break;

            case SymbolType.Integer _i:
                if (_i.Signed)
                    builder.Append("ly_int_t");
                else builder.Append("ly_uint_t");
                break;

            case SymbolType.SizedInteger _si:
            {
                // TODO(local): 128 is "supported" ?? but not like this without typedefs

                if (!_si.Signed)
                    builder.Append('u');
                builder.Append("int");

                switch (_si.BitCount)
                {
                    case 8: case 16: case 32: case 64:
                        builder.Append(_si.BitCount);
                        break;

                    default:
                    {
                        Console.WriteLine($"internal compiler error: writing type {type} not supported in C backend: unsupported bit width");
                        Environment.Exit(1);
                    } break;
                }

                builder.Append("_t");
            } break;

            case SymbolType.Float _f: builder.Append("double"); break;

            case SymbolType.String: builder.Append(m_cTypeNames[SymbolTypes.String]); break;

            case SymbolType.RawPtr: builder.Append("void*"); break;

            case SymbolType.Buffer bufferType:
            {
                AppendCType(builder, bufferType.ElementType);
                // TODO(local): do we need storage modifiers? like const? ideally we don't link against this code directly so it shouldn't matter
                builder.Append('*');
            } break;

            case SymbolType.Pointer pointerType:
            {
                AppendCType(builder, pointerType.ElementType);
                // TODO(local): do we need storage modifiers? like const? ideally we don't link against this code directly so it shouldn't matter
                builder.Append('*');
            } break;

            case SymbolType.Slice sliceType:
            {
                // NOTE(local): generate all slices as if they were read/write because we don't re-check access here
                sliceType = new SymbolType.Slice(sliceType.ElementType, AccessKind.ReadWrite);
                if (!m_cTypeNames.ContainsKey(sliceType))
                    GenerateCType(sliceType);

                builder.Append(m_cTypeNames[sliceType]);
            } break;

            case SymbolType.Dynamic: builder.Append("ly_dynamic_t"); break;

            case SymbolType.Struct structType:
            {
                if (!m_cTypeNames.ContainsKey(structType))
                    GenerateCType(structType);

                builder.Append(m_cTypeNames[structType]);
            } break;

            case SymbolType.Enum enumType:
            {
                if (!m_cTypeNames.ContainsKey(enumType))
                    GenerateCType(enumType);

                builder.Append(m_cTypeNames[enumType]);
            } break;

            case SymbolType.Union unionType:
            {
                if (!m_cTypeNames.ContainsKey(unionType))
                    GenerateCType(unionType);

                builder.Append(m_cTypeNames[unionType]);
            } break;

            default:
            {
                Console.WriteLine($"internal compiler error: writing type {type} not supported in C backend");
                Environment.Exit(1);
            } break;
        }
    }

    private void GenerateCType(SymbolType type)
    {
        switch (type)
        {
            case SymbolType.String stringType:
            {
                var stringBuilder = new StringBuilder();

                string stringName = $"ly_string";
                m_typedefs.AppendLine($"typedef struct {stringName} {stringName};");

                stringBuilder.AppendLine($"struct {stringName} {{");
                stringBuilder.AppendLine("  size_t length;");
                stringBuilder.AppendLine("  uint8_t* data;");
                stringBuilder.AppendLine("};");

                m_typeDecls.AppendLine(stringBuilder.ToString());

                m_cTypeNames[stringType] = stringName;
            } break;

            case SymbolType.Slice sliceType:
            {
                var sliceBuilder = new StringBuilder();

                string sliceName = $"ly_slice_{m_uniqueIndexCounter++}";
                m_typedefs.AppendLine($"typedef struct {sliceName} {sliceName}; /* laye type: {sliceType} */");

                sliceBuilder.AppendLine($"struct {sliceName} {{");
                sliceBuilder.AppendLine("  size_t length;");
                sliceBuilder.Append("  ");
                AppendCType(sliceBuilder, sliceType.ElementType);
                sliceBuilder.AppendLine("* data;");

                sliceBuilder.AppendLine("};");
                m_typeDecls.AppendLine(sliceBuilder.ToString());

                m_cTypeNames[sliceType] = sliceName;
            } break;

            case SymbolType.Struct structType:
            {
                var structBuilder = new StringBuilder();

                string structName = $"ly_{structType.Name}";
                m_typedefs.AppendLine($"typedef struct {structName} {structName};");

                structBuilder.AppendLine($"struct {structName} {{");
                if (structType.Fields.Length == 0)
                    structBuilder.AppendLine("  int dummy_;");
                else
                {
                    for (int i = 0; i < structType.Fields.Length; i++)
                    {
                        var field = structType.Fields[i];
                        structBuilder.Append("  ");
                        AppendCType(structBuilder, field.Type);
                        //structBuilder.Append(" ly_");
                        structBuilder.Append(' ');
                        structBuilder.Append(field.Name);
                        structBuilder.AppendLine(";");
                    }
                }

                structBuilder.AppendLine("};");
                m_typeDecls.AppendLine(structBuilder.ToString());

                m_cTypeNames[structType] = structName;
            } break;

            case SymbolType.Enum enumType:
            {
                var enumBuilder = new StringBuilder();

                string enumName = $"ly_{enumType.Name}";
                m_typedefs.AppendLine($"typedef enum {enumName} {enumName};");

                enumBuilder.AppendLine($"enum {enumName} {{");
                if (enumType.Variants.Length == 0)
                    enumBuilder.AppendLine($"  {enumName}__Dummy,");
                else
                {
                    for (int i = 0; i < enumType.Variants.Length; i++)
                    {
                        var (variantName, variantValue) = enumType.Variants[i];
                        enumBuilder.Append("  ");
                        enumBuilder.Append(enumName);
                        enumBuilder.Append("__");
                        enumBuilder.Append(variantName);
                        enumBuilder.Append(" = ");
                        enumBuilder.Append(variantValue);
                        enumBuilder.AppendLine(",");
                    }
                }

                enumBuilder.AppendLine("};");
                m_typeDecls.AppendLine(enumBuilder.ToString());

                m_cTypeNames[enumType] = enumName;

                enumBuilder.Clear();

                enumBuilder.Append("static ");
                AppendCType(enumBuilder, SymbolTypes.String);
                enumBuilder.Append(" ly_enum_to_string__");
                enumBuilder.Append(enumName);
                enumBuilder.AppendLine("(int enumValue) {");
                enumBuilder.AppendLine("    switch (enumValue) {");
                for (int i = 0; i < enumType.Variants.Length; i++)
                {
                    var (variantName, variantValue) = enumType.Variants[i];
                    enumBuilder.Append("    case ");
                    enumBuilder.Append(enumName);
                    enumBuilder.Append("__");
                    enumBuilder.Append(variantName);
                    enumBuilder.Append(": return LYSTR_STRING(\"");
                    enumBuilder.Append(variantName);
                    enumBuilder.Append("\", ");
                    enumBuilder.Append(Encoding.UTF8.GetByteCount(variantName));
                    enumBuilder.Append(')');
                    enumBuilder.AppendLine(";");
                }

                enumBuilder.AppendLine("    default: return LYSTR_STRING(\"<invalid>\", 9); ");
                enumBuilder.AppendLine("    }");
                enumBuilder.AppendLine("}");

                m_internalFunctionDecls.AppendLine(enumBuilder.ToString());
            } break;

            case SymbolType.Union unionType:
            {
                var unionBuilder = new StringBuilder();
                var unionEnumBuilder = new StringBuilder();

                string unionName = $"ly_{unionType.Name}";
                string unionEnumName = $"ly_{unionType.Name}_Kinds";
                m_typedefs.AppendLine($"typedef struct {unionName} {unionName};");
                //m_typedefs.AppendLine($"typedef enum {unionEnumName} {unionEnumName};");

                //

                unionEnumBuilder.AppendLine($"enum {unionEnumName} {{");
                if (unionType.Variants.Length == 0)
                    unionEnumBuilder.AppendLine($"  {unionEnumName}__Dummy = 1,");
                else
                {
                    for (int i = 0; i < unionType.Variants.Length; i++)
                    {
                        var variant = unionType.Variants[i];
                        unionEnumBuilder.Append("  ");
                        unionEnumBuilder.Append(unionEnumName);
                        unionEnumBuilder.Append("__");
                        unionEnumBuilder.Append(variant.Name);
                        if (i == 0)
                            unionEnumBuilder.Append(" = 1");
                        unionEnumBuilder.AppendLine(",");
                    }
                }

                unionEnumBuilder.AppendLine("};");
                m_typeDecls.AppendLine(unionEnumBuilder.ToString());

                //

                unionBuilder.AppendLine($"struct {unionName} {{");
                unionBuilder.AppendLine("  int kind;");
                unionBuilder.AppendLine("  union {");
                if (unionType.Variants.Length == 0)
                    unionBuilder.AppendLine($"    int dummy;");
                else
                {
                    for (int i = 0; i < unionType.Variants.Length; i++)
                    {
                        var variant = unionType.Variants[i];
                        if (variant.Fields.Length == 0) continue;
                        unionBuilder.AppendLine("    struct {");
                        for (int j = 0; j < variant.Fields.Length; j++)
                        {
                            var field = variant.Fields[j];
                            unionBuilder.Append("      ");
                            AppendCType(unionBuilder, field.Type);
                            //unionBuilder.Append(" ly_");
                            unionBuilder.Append(' ');
                            unionBuilder.Append(field.Name);
                            unionBuilder.AppendLine(";");
                        }

                        unionBuilder.Append("    } ");
                        unionBuilder.Append(variant.Name);
                        unionBuilder.AppendLine(";");
                    }
                }

                unionBuilder.AppendLine("  } variants;");
                unionBuilder.AppendLine("};");
                m_typeDecls.AppendLine(unionBuilder.ToString());

                m_cTypeNames[unionType] = unionName;

                //*
                unionBuilder.Clear();

                unionBuilder.Append("static ");
                AppendCType(unionBuilder, SymbolTypes.String);
                unionBuilder.Append(" ly_union_tag_to_string__");
                unionBuilder.Append(unionName);
                unionBuilder.AppendLine("(int enumValue) {");
                unionBuilder.AppendLine("    switch (enumValue) {");
                unionBuilder.AppendLine("    case 0: return LYSTR_STRING(\"nil\", 3);");
                for (int i = 0; i < unionType.Variants.Length; i++)
                {
                    var (variantName, variantValue) = unionType.Variants[i];
                    unionBuilder.Append("    case ");
                    unionBuilder.Append(unionEnumName);
                    unionBuilder.Append("__");
                    unionBuilder.Append(variantName);
                    unionBuilder.Append(": return LYSTR_STRING(\"");
                    unionBuilder.Append(variantName);
                    unionBuilder.Append("\", ");
                    unionBuilder.Append(Encoding.UTF8.GetByteCount(variantName));
                    unionBuilder.Append(')');
                    unionBuilder.AppendLine(";");
                }

                unionBuilder.AppendLine("    default: return LYSTR_STRING(\"<invalid>\", 9); ");
                unionBuilder.AppendLine("    }");
                unionBuilder.AppendLine("}");

                m_internalFunctionDecls.AppendLine(unionBuilder.ToString());
                //*/
            } break;

            default:
            {
                Console.WriteLine($"internal compiler error: attempt to generate type {type}");
                Environment.Exit(1);
            } break;
        }
    }

    // TODO(local): the read/write nature of container types means slicing a container may generate a struct type identical but incompatible with another identical slice type ...
    // container type generation should be figured out a bit better
    private void GenerateBufferSliceFunction(SymbolType.Buffer bufferType)
    {
        if (m_sliceOperationFunctionNames.ContainsKey(bufferType)) return;

        string functionName = $"ly_internal_bufferslice_{m_uniqueIndexCounter++}";

        var sliceType = new SymbolType.Slice(bufferType.ElementType, bufferType.Access);
        
        AppendCType(m_prototypes, sliceType);
        m_prototypes.Append($" {functionName}(");
        AppendCType(m_prototypes, bufferType);
        m_prototypes.AppendLine(" b_, ly_uint_t o_, ly_uint_t l_);");

        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.Append($" {functionName}(");
        AppendCType(m_internalFunctionDecls, bufferType);
        m_internalFunctionDecls.AppendLine(" b_, ly_uint_t o_, ly_uint_t l_)");
        m_internalFunctionDecls.AppendLine("{");
        m_internalFunctionDecls.Append("  ");
        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.AppendLine(" result;");
        m_internalFunctionDecls.AppendLine("  result.data = b_ + o_;");
        m_internalFunctionDecls.AppendLine("  result.length = l_;");
        m_internalFunctionDecls.AppendLine("  return result;");
        m_internalFunctionDecls.AppendLine("}");

        m_sliceOperationFunctionNames[bufferType] = functionName;
    }

    // TODO(local): the read/write nature of container types means slicing a container may generate a struct type identical but incompatible with another identical slice type ...
    // container type generation should be figured out a bit better
    private void GenerateSliceSliceFunction(SymbolType.Slice sliceType)
    {
        if (m_sliceOperationFunctionNames.ContainsKey(sliceType)) return;

        string functionName = $"ly_internal_sliceslice_{m_uniqueIndexCounter++}";

        AppendCType(m_prototypes, sliceType);
        m_prototypes.Append($" {functionName}(");
        AppendCType(m_prototypes, sliceType);
        m_prototypes.AppendLine(" b_, ly_uint_t o_, ly_uint_t l_);");

        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.Append($" {functionName}(");
        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.AppendLine(" s_, ly_uint_t o_, ly_uint_t l_)");
        m_internalFunctionDecls.AppendLine("{");
        m_internalFunctionDecls.Append("  ");
        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.AppendLine(" result;");
        m_internalFunctionDecls.AppendLine("  result.data = s_.data + o_;");
        m_internalFunctionDecls.AppendLine("  result.length = l_;");
        m_internalFunctionDecls.AppendLine("  return result;");
        m_internalFunctionDecls.AppendLine("}");

        m_sliceOperationFunctionNames[sliceType] = functionName;
    }

    // TODO(local): the read/write nature of container types means slicing a container may generate a struct type identical but incompatible with another identical slice type ...
    // container type generation should be figured out a bit better
    private void GenerateDynamicSliceFunction(SymbolType.Dynamic dynamicType)
    {
        if (m_sliceOperationFunctionNames.ContainsKey(dynamicType)) return;

        string functionName = $"ly_internal_dynamicslice_{m_uniqueIndexCounter++}";

        var sliceType = new SymbolType.Slice(dynamicType.ElementType, dynamicType.Access);

        AppendCType(m_prototypes, sliceType);
        m_prototypes.Append($" {functionName}(");
        AppendCType(m_prototypes, dynamicType);
        m_prototypes.AppendLine(" d_, ly_uint_t o_, ly_uint_t l_);");

        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.Append($" {functionName}(");
        AppendCType(m_internalFunctionDecls, dynamicType);
        m_internalFunctionDecls.AppendLine(" d_, ly_uint_t o_, ly_uint_t l_)");
        m_internalFunctionDecls.AppendLine("{");
        m_internalFunctionDecls.Append("  ");
        AppendCType(m_internalFunctionDecls, sliceType);
        m_internalFunctionDecls.AppendLine(" result;");
        m_internalFunctionDecls.Append("  ");
        AppendCType(m_internalFunctionDecls, dynamicType.ElementType);
        m_internalFunctionDecls.Append("* data = (");
        AppendCType(m_internalFunctionDecls, dynamicType.ElementType);
        m_internalFunctionDecls.AppendLine("*)d_.data;");
        m_internalFunctionDecls.Append("  result.data = malloc(l_ * sizeof(");
        AppendCType(m_internalFunctionDecls, dynamicType.ElementType);
        m_internalFunctionDecls.AppendLine("));");
        m_internalFunctionDecls.AppendLine("  memcpy(result.data, data + o_, l_);");
        m_internalFunctionDecls.AppendLine("  result.length = l_;");
        m_internalFunctionDecls.AppendLine("  return result;");
        m_internalFunctionDecls.AppendLine("}");

        m_sliceOperationFunctionNames[dynamicType] = functionName;
    }

    private void AppendSymbolName(StringBuilder builder, Symbol symbol)
    {
        if (!m_symbolNames.TryGetValue(symbol, out string? name))
        {
            string symbolName = symbol.Name;
            if (ReservedNames.Contains(symbolName))
                symbolName = $"ly_{symbolName}_{m_symbolNames.Count}";

            m_symbolNames[symbol] = name = symbolName;
        }

        builder.Append(name);
    }

    private void AppendFunctionPrototype(StringBuilder builder, Symbol.Function sym)
    {
        string functionName = m_symbolNames[sym];

        AppendCType(builder, sym.Type!.ReturnType);
        builder.Append(' ');
        builder.Append(functionName);
        builder.Append('(');
        for (int i = 0; i < sym.Type.Parameters.Length; i++)
        {
            var p = sym.Type.Parameters[i];
            if (i > 0)
                builder.Append(", ");

            AppendCType(builder, p.Type);
            builder.Append(' ');
            builder.Append(p.Name);
        }

        if (sym.Type.VarArgs == VarArgsKind.C)
        {
            if (sym.Type.Parameters.Length > 0)
                builder.Append(", ");
            builder.Append("...");
        }

        builder.Append(')');
    }

    private void CompileFunction(LayeCst.FunctionDeclaration fnDecl)
    {
        string name = fnDecl.FunctionSymbol.Name;
        if (ReservedNames.Contains(name) && name != "main" && name != "_start")
            return;

        AppendFunctionPrototype(m_prototypes, fnDecl.FunctionSymbol);
        m_prototypes.AppendLine(";");

        if (fnDecl.Body is LayeCst.EmptyFunctionBody)
            return;

        AppendFunctionPrototype(m_functionDecls, fnDecl.FunctionSymbol);
        m_functionDecls.AppendLine(" {");
        switch (fnDecl.Body)
        {
            case LayeCst.BlockFunctionBody blockBody:
            {
                m_tabs++;
                foreach (var stmt in blockBody.BodyBlock.Body)
                {
                    CompileStatement(m_functionDecls, stmt);
                }

                m_tabs--;
            } break;
        }

        m_functionDecls.AppendLine("}");
        m_functionDecls.AppendLine();
    }

    private void CompileStatement(StringBuilder builder, LayeCst.Stmt stmt)
    {
        AppendTabs(builder);
        switch (stmt)
        {
            case LayeCst.ExpressionStatement exprStmt:
            {
                CompileExpression(builder, exprStmt.Expression);
                builder.AppendLine(";");
            } break;

            case LayeCst.BindingDeclaration bindingDecl:
            {
                AppendCType(builder, bindingDecl.BindingSymbol.Type!);
                builder.Append(' ');
                AppendSymbolName(builder, bindingDecl.BindingSymbol);
                builder.Append(" = ");
                if (bindingDecl.Expression is not null)
                    CompileExpression(builder, bindingDecl.Expression);
                else builder.Append("{0}");
                builder.AppendLine(";");
            } break;

            case LayeCst.Assignment assignStmt:
            {
                CompileExpression(builder, assignStmt.TargetExpression);
                builder.Append(" = ");
                CompileExpression(builder, assignStmt.ValueExpression);
                builder.AppendLine(";");
            } break;

            case LayeCst.DynamicAppend dynAppendStmt:
            {
                string tempName = $"ly_dyn_{m_uniqueIndexCounter++}";

                builder.Append("ly_dynamic_t* ");
                builder.Append(tempName);
                builder.Append(" = &(");
                CompileExpression(builder, dynAppendStmt.TargetExpression);
                builder.AppendLine(");");

                AppendTabs(builder);
                builder.Append("ly_internal_dynamic_ensure_capacity(");
                builder.Append(tempName);
                builder.Append(", (");
                builder.Append(tempName);
                builder.Append("->count + 1) * sizeof(");
                AppendCType(builder, dynAppendStmt.ValueExpression.Type); // if type checking succeeded, this type is the same as the container element type
                builder.AppendLine("));");

                AppendTabs(builder);
                builder.Append("(/*cast*/(");
                AppendCType(builder, dynAppendStmt.ValueExpression.Type);
                builder.Append("*) (");
                builder.Append(tempName);
                builder.Append("->data))[");
                builder.Append(tempName);
                builder.Append("->count] = (");
                CompileExpression(builder, dynAppendStmt.ValueExpression);
                builder.AppendLine(");");

                AppendTabs(builder);
                builder.Append(tempName);
                builder.AppendLine("->count++;");
            } break;

            case LayeCst.Block blockStmt:
            {
                builder.AppendLine("{");
                m_tabs++;
                foreach (var childStmt in blockStmt.Body)
                    CompileStatement(builder, childStmt);

                m_tabs--;
                AppendTabs(builder);
                builder.AppendLine("}");
            } break;

            case LayeCst.If ifStmt:
            {
                builder.Append("if (");
                CompileExpression(builder, ifStmt.Condition);
                builder.AppendLine(")");

                bool isBlockBody = ifStmt.IfBody is LayeCst.Block;

                if (!isBlockBody) m_tabs++;
                CompileStatement(builder, ifStmt.IfBody);
                if (!isBlockBody) m_tabs--;

                if (ifStmt.ElseBody is not null)
                {
                    AppendTabs(builder);
                    builder.Append("else");

                    int tabs = m_tabs;
                    if (ifStmt.ElseBody is LayeCst.If)
                    {
                        //m_tabs = 0;
                        builder.Append(' ');
                        CompileStatement(builder, ifStmt.ElseBody);
                        //m_tabs = tabs;
                    }
                    else
                    {
                        isBlockBody = ifStmt.ElseBody is LayeCst.Block;
                        if (!isBlockBody)
                        {
                            m_tabs++;
                            builder.AppendLine();
                        }
                        else
                        {
                            m_tabs = 0;
                            builder.Append(' ');
                        }

                        CompileStatement(builder, ifStmt.ElseBody);
                        if (!isBlockBody)
                            m_tabs--;
                        else m_tabs = tabs;
                    }
                }
            } break;

            case LayeCst.While whileStmt:
            {
                if (whileStmt.ElseBody is not null)
                {
                    Console.WriteLine("internal compiler error: attempt to compile while statement with else block");
                    Environment.Exit(1);
                    return;
                }

                builder.Append("while (");
                CompileExpression(builder, whileStmt.Condition);
                builder.AppendLine(")");

                bool isBlockBody = whileStmt.WhileBody is LayeCst.Block;

                if (!isBlockBody) m_tabs++;
                CompileStatement(builder, whileStmt.WhileBody);
                if (!isBlockBody) m_tabs--;
            } break;

            case LayeCst.Return returnStmt:
            {
                builder.Append("return");
                if (returnStmt.ReturnValue is not null)
                {
                    builder.Append('(');
                    CompileExpression(builder, returnStmt.ReturnValue);
                    builder.Append(')');
                }

                builder.AppendLine(";");
            } break;

            case LayeCst.Break: builder.AppendLine("break;"); break;

            default:
            {
                builder.AppendLine($"/* unknown statement {stmt.GetType().Name} */");
                //Console.WriteLine($"internal compiler error: attempt to compile unknown statement type {stmt.GetType().Name}");
                //Environment.Exit(1);
            } break;
        }
    }

    private void CompileExpression(StringBuilder builder, LayeCst.Expr expr)
    {
        void AppendBinary(LayeCst.Expr left, LayeCst.Expr right, string op)
        {
            builder.Append('(');
            CompileExpression(builder, left);
            builder.Append(") ");
            builder.Append(op);
            builder.Append(" (");
            CompileExpression(builder, right);
            builder.Append(')');
        }

        switch (expr)
        {
            case LayeCst.Bool boolExpr: builder.Append(boolExpr.Literal.Kind == Keyword.True ? "true" : "false"); break;
            case LayeCst.NullPtr boolExpr: builder.Append("NULL"); break;
            case LayeCst.Integer intExpr: builder.Append(intExpr.Literal.LiteralValue); break;
            case LayeCst.Float floatExpr: builder.Append(floatExpr.Literal.LiteralValue); break;

            case LayeCst.String stringExpr:
            {
                switch (stringExpr.Type)
                {
                    case SymbolType.Buffer:
                    {
                        builder.Append('"');
                        builder.Append(stringExpr.Literal.LiteralValue);
                        builder.Append('"');
                    } break;

                    case SymbolType.String:
                    {
                        builder.Append("LYSTR_STRING(\"");
                        builder.Append(stringExpr.Literal.LiteralValue);
                        builder.Append("\", ");
                        builder.Append(Encoding.UTF8.GetByteCount(stringExpr.Literal.LiteralValue));
                        builder.Append(')');
                    } break;
                    
                    case SymbolType.Slice:
                    {
                        builder.Append("LYSTR_SLICE(\"");
                        builder.Append(stringExpr.Literal.LiteralValue);
                        builder.Append("\", ");
                        builder.Append(Encoding.UTF8.GetByteCount(stringExpr.Literal.LiteralValue));
                        builder.Append(')');
                    } break;
                }
            } break;

            case LayeCst.LoadValue loadExpr: AppendSymbolName(builder, loadExpr.Symbol); break;

            case LayeCst.LoadEnumVariant loadEnumVariantExpr:
            {
                AppendCType(builder, loadEnumVariantExpr.EnumSymbol.Type!);
                builder.Append("__");
                builder.Append(loadEnumVariantExpr.VariantName);
            } break;

            case LayeCst.LoadUnionVariant loadUnionVariantExpr:
            {
                var unionSymbol = loadUnionVariantExpr.UnionSymbol;
                //string variantKindName = $"{m_cTypeNames[unionSymbol.Type!]}_Kinds";
                string unionTypeName = m_cTypeNames[unionSymbol.Type!];
                string variantKindName = $"{unionTypeName}_Kinds__{loadUnionVariantExpr.Variant.Name}";

                builder.Append('(');
                // AppendCType(builder, unionSymbol.Type!);
                builder.Append(unionTypeName);
                builder.Append("){.kind = ");
                builder.Append(variantKindName);
                for (int i = 0; i < loadUnionVariantExpr.Arguments.Length; i++)
                {
                    var arg = loadUnionVariantExpr.Arguments[i];

                    builder.Append(", .variants.");
                    builder.Append(loadUnionVariantExpr.Variant.Name);
                    builder.Append('.');
                    builder.Append(loadUnionVariantExpr.Variant.Fields[i].Name);
                    builder.Append(" = ");
                    CompileExpression(builder, arg);
                }

                builder.Append('}');
            } break;

            case LayeCst.LoadUnionNilVariant loadUnionVariantExpr2:
            {
                var unionSymbol = loadUnionVariantExpr2.UnionSymbol;
                string unionTypeName = m_cTypeNames[unionSymbol.Type!];

                builder.Append('(');
                // AppendCType(builder, unionSymbol.Type!);
                builder.Append(unionTypeName);
                builder.Append("){.kind = 0}");
            } break;

            case LayeCst.SliceLengthLookup sliceLengthExpr:
            {
                builder.Append('(');
                CompileExpression(builder, sliceLengthExpr.TargetExpression);
                builder.Append(").length");
            } break;

            case LayeCst.StringLengthLookup strlenExpr:
            {
                builder.Append('(');
                CompileExpression(builder, strlenExpr.TargetExpression);
                builder.Append(").length");
            } break;

            case LayeCst.StringDataLookup strdataExpr:
            {
                builder.Append('(');
                CompileExpression(builder, strdataExpr.TargetExpression);
                builder.Append(").data");
            } break;

            case LayeCst.DynamicLengthLookup dynlenExpr:
            {
                builder.Append('(');
                CompileExpression(builder, dynlenExpr.TargetExpression);
                builder.Append(").count");
            }
            break;

            case LayeCst.DynamicIndex dynExpr:
            {
                Debug.Assert(dynExpr.Arguments.Length == 1);

                if (dynExpr.TargetExpression.Type is SymbolType.Dynamic dynamicType)
                {
                    builder.Append("(/*cast*/(");
                    AppendCType(builder, dynamicType.ElementType);
                    builder.Append("*) (");
                    CompileExpression(builder, dynExpr.TargetExpression);
                    builder.Append(").data)");

                    builder.Append("[");
                    CompileExpression(builder, dynExpr.Arguments[0]);
                    builder.Append("]");
                }
                else
                {
                    builder.Append('(');
                    CompileExpression(builder, dynExpr.TargetExpression);
                    builder.Append(')');

                    switch (dynExpr.TargetExpression.Type)
                    {
                        case SymbolType.String stringTarget: builder.Append(".data"); break;
                        case SymbolType.Slice sliceTarget: builder.Append(".data"); break;
                    }

                    builder.Append('[');
                    CompileExpression(builder, dynExpr.Arguments[0]);
                    builder.Append(']');
                }
            } break;

            case LayeCst.NamedIndex namedExpr:
            {
                builder.Append('(');
                CompileExpression(builder, namedExpr.TargetExpression);
                builder.Append(").");
                // TODO(local): we have symbol names, but we need something for this as well (or to make these symbols instead)
                builder.Append(namedExpr.Name.Image);
            } break;

            case LayeCst.SizeOf sizeofExpr:
            {
                builder.Append("sizeof(");
                AppendCType(builder, sizeofExpr.TargetType);
                builder.Append(')');
            } break;

            case LayeCst.NameOfEnumVariant nameofEnumExpr:
            {
                builder.Append("ly_enum_to_string__");
                AppendCType(builder, nameofEnumExpr.EnumType);
                builder.Append('(');
                CompileExpression(builder, nameofEnumExpr.EnumValueExpression);
                builder.Append(')');
            } break;

            case LayeCst.NameOfUnionVariant nameofUnionExpr:
            {
                builder.Append("LYSTR_STRING(\"");
                builder.Append(nameofUnionExpr.VariantType.Name);
                builder.Append("\", ");
                builder.Append(Encoding.UTF8.GetByteCount(nameofUnionExpr.VariantType.Name));
                builder.Append(')');
            } break;

            case LayeCst.NameOfUnionVariantExpression nameofUnionExpr2:
            {
                builder.Append("ly_union_tag_to_string__");
                AppendCType(builder, nameofUnionExpr2.UnionType);
                builder.Append("((");
                CompileExpression(builder, nameofUnionExpr2.VariantExpression);
                builder.Append(").kind");
                builder.Append(')');
            } break;

            case LayeCst.Negate negExpr:
            {
                builder.Append("-(");
                CompileExpression(builder, negExpr.Expression);
                builder.Append(')');
            } break;

            case LayeCst.LogicalNot logNotExpr:
            {
                builder.Append("!(");
                CompileExpression(builder, logNotExpr.Expression);
                builder.Append(')');
            } break;
            
            case LayeCst.BitwiseComplement complExpr:
            {
                builder.Append("~(");
                CompileExpression(builder, complExpr.Expression);
                builder.Append(')');
            } break;
            
            case LayeCst.ValueAt derefExpr:
            {
                builder.Append("*(");
                CompileExpression(builder, derefExpr.Expression);
                builder.Append(')');
            } break;
            
            case LayeCst.AddressOf addrExpr:
            {
                builder.Append("&(");
                CompileExpression(builder, addrExpr.Expression);
                builder.Append(')');
            } break;

            case LayeCst.Add addExpr: AppendBinary(addExpr.LeftExpression, addExpr.RightExpression, "+");  break;
            case LayeCst.Subtract subExpr: AppendBinary(subExpr.LeftExpression, subExpr.RightExpression, "-");  break;
            case LayeCst.Multiply multExpr: AppendBinary(multExpr.LeftExpression, multExpr.RightExpression, "*");  break;
            case LayeCst.Divide divExpr: AppendBinary(divExpr.LeftExpression, divExpr.RightExpression, "/");  break;
            case LayeCst.Remainder remExpr: AppendBinary(remExpr.LeftExpression, remExpr.RightExpression, "%"); break;

            case LayeCst.LeftShift lshExpr: AppendBinary(lshExpr.LeftExpression, lshExpr.RightExpression, "<<"); break;
            case LayeCst.RightShift rshExpr: AppendBinary(rshExpr.LeftExpression, rshExpr.RightExpression, ">>"); break;
            case LayeCst.BitwiseAnd andExpr: AppendBinary(andExpr.LeftExpression, andExpr.RightExpression, "&"); break;
            case LayeCst.BitwiseOr orExpr: AppendBinary(orExpr.LeftExpression, orExpr.RightExpression, "|"); break;
            case LayeCst.BitwiseXor xorExpr: AppendBinary(xorExpr.LeftExpression, xorExpr.RightExpression, "^"); break;

            case LayeCst.LogicalAnd andExpr: AppendBinary(andExpr.LeftExpression, andExpr.RightExpression, "&&"); break;
            case LayeCst.LogicalOr orExpr: AppendBinary(orExpr.LeftExpression, orExpr.RightExpression, "||"); break;

            case LayeCst.CompareEqual eqExpr: AppendBinary(eqExpr.LeftExpression, eqExpr.RightExpression, "=="); break;
            case LayeCst.CompareNotEqual eqExpr: AppendBinary(eqExpr.LeftExpression, eqExpr.RightExpression, "!="); break;
            case LayeCst.CompareLess lessExpr: AppendBinary(lessExpr.LeftExpression, lessExpr.RightExpression, "<");  break;
            case LayeCst.CompareLessEqual lessEqExpr: AppendBinary(lessEqExpr.LeftExpression, lessEqExpr.RightExpression, "<=");  break;
            case LayeCst.CompareGreater greaterExpr: AppendBinary(greaterExpr.LeftExpression, greaterExpr.RightExpression, ">"); break;
            case LayeCst.CompareGreaterEqual greaterEqExpr: AppendBinary(greaterEqExpr.LeftExpression, greaterEqExpr.RightExpression, ">="); break;

            case LayeCst.CompareUnionToNil compareUnionToNilExpr:
            {
                builder.Append('(');
                CompileExpression(builder, compareUnionToNilExpr.Expression);
                builder.Append(").kind == 0");
            } break;

            case LayeCst.TypeCast castExpr:
            {
                builder.Append('(');
                AppendCType(builder, castExpr.Type);
                builder.Append(')');
                CompileExpression(builder, castExpr.Expression);
            } break;

            case LayeCst.UnionVariantDowncast unionVariantDowncastExpr:
            {
                CompileExpression(builder, unionVariantDowncastExpr.Expression);
            } break;

            case LayeCst.SliceToString sliceToStringExpr:
            {
                builder.Append("ly_internal_slicetostring(");
                CompileExpression(builder, sliceToStringExpr.SliceExpression);
                builder.Append(')');
            } break;

            case LayeCst.Substring substringExpr:
            {
                builder.Append("ly_internal_substring(");
                CompileExpression(builder, substringExpr.TargetExpression);
                builder.Append(", ");
                if (substringExpr.OffsetExpression is not null)
                    CompileExpression(builder, substringExpr.OffsetExpression);
                else builder.Append('0');
                builder.Append(", ");
                if (substringExpr.CountExpression is not null)
                    CompileExpression(builder, substringExpr.CountExpression);
                else
                {
                    builder.Append('(');
                    CompileExpression(builder, substringExpr.TargetExpression);
                    builder.Append(").length");
                    if (substringExpr.OffsetExpression is not null)
                    {
                        builder.Append("- (");
                        CompileExpression(builder, substringExpr.OffsetExpression);
                        builder.Append(')');
                    }
                }

                builder.Append(')');
            } break;

            case LayeCst.Slice sliceExpr:
            {
                switch (sliceExpr.TargetExpression.Type)
                {
                    case SymbolType.Buffer bufferType:
                    {
                        GenerateBufferSliceFunction(bufferType);
                        string bufferSliceFunctionName = m_sliceOperationFunctionNames[bufferType];

                        builder.Append(bufferSliceFunctionName);
                        builder.Append('(');
                        CompileExpression(builder, sliceExpr.TargetExpression);
                        builder.Append(", ");
                        if (sliceExpr.OffsetExpression is not null)
                            CompileExpression(builder, sliceExpr.OffsetExpression);
                        else builder.Append('0');
                        builder.Append(", ");
                        Debug.Assert(sliceExpr.CountExpression is not null);
                        CompileExpression(builder, sliceExpr.CountExpression!);
                        builder.Append(')');
                    } break;
                    
                    // TODO(local): sadly this breaks if there are side effects, so we would want to (in future) generate this from an IR instead : (
                    case SymbolType.Slice sliceType:
                    {
                        GenerateSliceSliceFunction(sliceType);
                        string sliceSliceFunctionName = m_sliceOperationFunctionNames[sliceType];

                        builder.Append(sliceSliceFunctionName);
                        builder.Append('(');
                        CompileExpression(builder, sliceExpr.TargetExpression);
                        builder.Append(", ");
                        if (sliceExpr.OffsetExpression is not null)
                            CompileExpression(builder, sliceExpr.OffsetExpression);
                        else builder.Append('0');
                        builder.Append(", ");
                        if (sliceExpr.CountExpression is not null)
                            CompileExpression(builder, sliceExpr.CountExpression);
                        else
                        {
                            builder.Append('(');
                            CompileExpression(builder, sliceExpr.TargetExpression);
                            builder.Append(").length");
                            if (sliceExpr.OffsetExpression is not null)
                            {
                                builder.Append("- (");
                                CompileExpression(builder, sliceExpr.OffsetExpression);
                                builder.Append(')');
                            }
                        }

                        builder.Append(')');
                    } break;

                    case SymbolType.Dynamic dynamicType:
                    {
                        GenerateDynamicSliceFunction(dynamicType);
                        string bufferSliceFunctionName = m_sliceOperationFunctionNames[dynamicType];

                        builder.Append(bufferSliceFunctionName);
                        builder.Append('(');
                        CompileExpression(builder, sliceExpr.TargetExpression);
                        builder.Append(", ");
                        if (sliceExpr.OffsetExpression is not null)
                            CompileExpression(builder, sliceExpr.OffsetExpression);
                        else builder.Append('0');
                        builder.Append(", ");
                        Debug.Assert(sliceExpr.CountExpression is not null);
                        CompileExpression(builder, sliceExpr.CountExpression!);
                        builder.Append(')');
                    } break;
                }
            } break;

            case LayeCst.InvokeFunction invokeExpr:
            {
                AppendSymbolName(builder, invokeExpr.TargetFunctionSymbol);
                builder.Append('(');
                for (int i = 0; i < invokeExpr.Arguments.Length; i++)
                {
                    if (i > 0) builder.Append(", ");
                    CompileExpression(builder, invokeExpr.Arguments[i]);
                }

                builder.Append(')');
            } break;

            default:
            {
                builder.Append($"/* {expr.GetType().Name} */");
                //Console.WriteLine($"internal compiler error: attempt to compile unknown expression type {expr.GetType().Name}");
                //Environment.Exit(1);
            } break;
        }
    }
}
