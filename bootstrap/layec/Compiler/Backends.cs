namespace laye.Compiler;

[Flags]
public enum Backends
{
    None = 0,

    C,
    Msil,
    Llvm,
}
