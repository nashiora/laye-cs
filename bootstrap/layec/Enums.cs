namespace laye;

// uses the same ordering as Odin
// https://github.com/odin-lang/Odin/blob/master/core/runtime/core.odin
public enum CallingConvention
{
    Invalid = 0,

    Laye = 1,
    LayeNoContext = 2,

    CDecl = 3,
    StdCall = 4,
    FastCall = 5,

    None = 6,
    Naked = 7,
}

public enum VarArgsKind
{
    None = 0,
    Laye,
    C,
}

public enum FunctionHintKind
{
    None,
    Intrinsic,
    Export,
    Extern,
    Inline,
    Naked,
}

public enum VisibilityKind
{
    /// <summary>
    /// Public marks an entity as visible outside of its own project
    /// </summary>
    Public,
    /// <summary>
    ///  Internal marks an entity as visible only within the current project
    /// </summary>
    Internal,
    /// <summary>
    /// Private marks an entity as visible only within the current file
    /// </summary>
    Private,
}

public enum AccessKind
{
    ReadWrite,
    ReadOnly,
    WriteOnly,
    Constant,
}
