/* ===== Diagnostics =====
 *
 * Diagnostics are reports of information, warnings and errors in the source text.
 * 
 * The base diagnostic data structures don't do anything fancy: if we want to point out 
 *
 *
 */

struct diagnostic
{
    string message;
    /* Optional string of any (optionally annotated) source text associated with the diagnostic.
     * Separate from message so editors don't have massive errors to display or parse.
     * Also separate from the text you can get out of the associated source span so that the text view and annotations can be context driven.
     */
    string sourceText;
    diagnostic_source_kind sourceKind;
    diagnostic_kind kind;
}

enum diagnostic_source_kind
{
    span(source_span sourceSpan),
    token(syntax_token token),
    trivia(syntax_trivia trivia),
    //syntax(syntax_node node),
}

enum diagnostic_kind
{
    info, warning, error
}

struct diagnostic_bag
{
    diagnostic[dynamic] diagnostics;
}

source_span diagnostic_source_span_get(diagnostic d)
{
    if (d.sourceKind is ::span spanKind) return spanKind.sourceSpan;
    if (d.sourceKind is ::token tokenKind) return tokenKind.token.sourceSpan;
    if (d.sourceKind is ::trivia triviaKind) return triviaKind.trivia.sourceSpan;
    //if (d.sourceKind is ::node nodeKind) return nodeKind.node.sourceSpan;
    //if (d.sourceKind is ::ir irKind) return irKind.node.sourceSpan;

    // NOTE(local): switch
    // NOTE(local): unreachable
    panic("unhandled case in diagnostic_source_span_get");

    // compiler requires return value
    source_span noSpan;
    return noSpan;
}

void diagnostics_add_info(diagnostic_bag *b, diagnostic_source_kind sourceKind, string message)
{
    diagnostic d;
    d.message = message;
    d.sourceKind = sourceKind;
    d.kind = ::info;
    dynamic_append(b.diagnostics, d);
}

void diagnostics_add_warning(diagnostic_bag *b, diagnostic_source_kind sourceKind, string message)
{
    diagnostic d;
    d.message = message;
    d.sourceKind = sourceKind;
    d.kind = ::warning;
    dynamic_append(b.diagnostics, d);
}

void diagnostics_add_error(diagnostic_bag *b, diagnostic_source_kind sourceKind, string message)
{
    diagnostic d;
    d.message = message;
    d.sourceKind = sourceKind;
    d.kind = ::error;
    dynamic_append(b.diagnostics, d);
}

/* ===== Tokens =====
 *
 *
 *
 *
 */

struct syntax_trivia
{
    source_span sourceSpan;
    syntax_trivia_kind kind;
}

struct syntax_token
{
    source_span sourceSpan;

    syntax_trivia[] leadingTrivia;
    syntax_trivia[] trailingTrivia;

    syntax_token_kind kind;
}

enum syntax_trivia_kind
{
    invalid,
    comment_line,
    comment_block,
    white_space,
}

enum syntax_token_kind
{
    eof,
    poison_token,

    ident(string image), // the image should be the same as `source_span_to_string(token.sourceSpan)`, but we don't want to recompute that all the time

    // NOTE(local): a literal doesn't necessarily need to contain the value itself, we can re-parse it before code generation. see if you want to change this
    literal_integer(u64 value),
    literal_float(float value),
    literal_character(i32 value),
    literal_string(string value),

    /*
     * TODO(local): figure out how to handle interpolated strings. below is an example of how I think I'd do C# style interpolation
     * If split up like this into completely separate tokens, the lexer needs to be able to return multiple tokens or access the token storage directly, so
     *   maybe instead a single literal_interpolated_string token with more additional sub-tokens inside would be easier? or a lexer state, like
     *   `normal`, `interpolated_string`, `interpolated_string_tokens` or smth to change token reading rules
     */
    //interpolated_string_begin, /* start `$"` in `$"hello, {world}!"` */
    //interpolated_string_literal, /* `hello, ` or `!` in `$"hello, {world}!"` */
    //interpolated_string_expr_begin, /* `{` in `$"hello, {world}!"` */
    //interpolated_string_expr_end, /* `}` in `$"hello, {world}!"` */
    //interpolated_string_end, /* end `"` or `!` in `$"hello, {world}!"` */

    /* delimiter tokens */
    open_paren,
    close_paren,
    open_bracket,
    close_bracket,
    open_brace,
    close_brace,
    hash_open_bracket,

    comma,
    dot,
    question,

    colon,
    semi_colon,

    colon_colon,
    equal_greater,
    question_dot,

    back_slash,

    /* operator tokens */
    equal,

    plus, minus,
    star, slash,
    percent,

    amp, pipe,
    tilde, caret,

    equal_equal, bang_equal,
    less, greater,
    less_equal, greater_equal,
    bang_less, bang_greater,
    bang_less_equal, bang_greater_equal,

    less_less, greater_greater,

    plus_equal, minus_equal,
    star_equal, slash_equal,
    percent_equal,

    amp_equal, pipe_equal,
    caret_equal,

    less_less_equal, greater_greater_equal,

    amp_amp, pipe_pipe,

    /* keyword tokens */
    kw_and, kw_or,
    kw_xor, kw_not,

    kw_cast, kw_is, // NOTE(local): `is` will probably be supported to have expressions like in C# so that nilable types don't require a comparison/cast or switch
    kw_sizeof, kw_offsetof,
    kw_nameof, kw_nameof_variant, // NOTE(local): `nameof_variant` is a holdover from the bootstrap compiler where I don't yet have a solution to this. At least this can be grepped and replaced in the future instead of a special cast rule : )
    kw_dynamic_append, kw_dynamic_free,
    kw_defer,

    kw_void, kw_var,
    kw_int, kw_uint,
    kw_int_sized(u16 size), kw_uint_sized(u16 size),
    kw_int_least_sized(u16 size), kw_uint_least_sized(u16 size),
    kw_bool, kw_float,
    kw_bool_sized(u16 size), kw_float_sized(u16 size),
    kw_rune, kw_string,
    kw_rawptr, kw_dynamic,
    kw_noreturn, kw_varargs,
    kw_struct, kw_enum, kw_union,

    kw_signed, kw_unsigned,
    kw_char, kw_short, kw_long,

    kw_const,
    kw_readonly, kw_writeonly,

    kw_true, kw_false,
    kw_nil, kw_nullptr,
    kw_context, kw_noinit,
    kw_target,

    kw_global, kw_namespace, kw_using,

    kw_extern, kw_foreign,
    kw_public, kw_private,
    // kw_internal, // internal by default, so no keyword for it?

    /* NOTE(local): use callconv(::variant) instead? easier to add new calling conventions without keywords */
    kw_callconv,
    //kw_cdecl, kw_nocontext,
    //kw_stdcall, kw_fastcall,
    kw_naked, /* removes any compiler-generated functionality to the function */
    // TODO(local): are there any functions we specifically need to be intrinsics AND defined in the language?
    kw_intrinsic,
    // TODO(local): how do we want to handle DLLs? is `export` the only thing or are there other cases that can remove keywords?
    kw_export,
    kw_inline,

    kw_if, kw_else,
    kw_while, kw_for,
    kw_switch, kw_case,
    kw_default, /* do we need `default` in Laye? `case:` serves the same purpose, and we don't need `= default` since we want everything to be default if set to 0 */
    kw_return, kw_yield, /* yield is probably only necessary when we get generators so can likely be ignored for a while. good to reserve it, though */
    kw_break, kw_continue,
    kw_unreachable,
}

/* ===== Syntax Tree =====
 *
 *
 *
 *
 */

 struct syntax_node
 {
    source_span sourceSpan;
    symbol_type_ref typeRef; /* typeRef will be invalid for non-expression nodes or nodes with no yet-resolved type. */
    syntax_node_kind kind;
 }

 enum syntax_node_kind
 {
    // ===== Identifier Nodes

    /* This is not an error during parsing. Represents an identifier with no yet-known meaning.
     * If this node cannot be replaced with a resolved identifier node during checking then an unresolved identifier error is issued. */
    unresolved_identifier(syntax_token identifier),

    // TODO(local): other identifier node types

    // ===== Literal Nodes

    untyped_literal_integer(syntax_token literal), /* A literal integer with no type information. */
    typed_literal_integer(syntax_token literal), /* A literal integer with known and validated type information. */
    /* A literal integer with known but invalid type information.
     * For example, in `i8 v = 800;` contains `800` the type can be known to be `i8`, but 800 is far above the signed 8-bit maximum.
     * A bad assignment will also lead to a bad typed literal. `string s = 10;` will result in a literal integer typed with the default integer type `uint`. */
    bad_typed_literal_integer(syntax_token literal),

    untyped_literal_float(syntax_token literal),
    typed_literal_float(syntax_token literal),
    /* A literal float with known but invalid type information. The same functionality as bad_typed_literal_integer for floats. */
    bad_typed_literal_float(syntax_token literal),

    untyped_literal_string(syntax_token literal), /* A literal string with no type information. */
    typed_literal_string(syntax_token literal), /* A literal string with known and validated type information. */
    /* A literal string with known but invalid type information. The same functionality as bad_typed_literal_integer for strings. */
    bad_typed_literal_string(syntax_token literal),

    untyped_literal_bool(syntax_token literal), /* A literal bool with no type information. */
    typed_literal_bool(syntax_token literal), /* A literal bool with known type information. */
    /* A literal bool with known but invalid type information. The same functionality as bad_typed_literal_integer for bools. */
    bad_typed_literal_bool(syntax_token literal),

    /* A literal rune with no type information. Some representation of a unicode codepoint by default.
     * (Note that rune literals are type `rune` by default. It's entirely possible that we allow a rune literal to implicitly convert to a single byte (i8/u8)
     *  if it's in range, for anything working with ASCII for example, or to byte arrays or slices since those can be constructed at compile time.) */
    untyped_literal_rune(syntax_token literal),
    typed_literal_rune(syntax_token literal), /* A literal rune with known type information. */
    /* A literal rune with known but invalid type information. The same functionality as bad_typed_literal_integer for runes. */
    bad_typed_literal_rune(syntax_token literal),

    /* A literal nil with no type information. Nil is used for nilable types and union variants.
     * This does not refer to the explicit union variant syntax `*::nil` though it can still be implicitly converted to it. */
    untyped_literal_nil(syntax_token literal),
    typed_literal_nil(syntax_token literal), /* A literal nil with known type information. This includes union variant nil conversion. */
    /* A literal nil with known but invalid type information. The same functionality as bad_typed_literal_integer for nils. */
    bad_typed_literal_nil(syntax_token literal),

    untyped_literal_nullptr(syntax_token literal), /* A literal nullptr with no type information. */
    typed_literal_nullptr(syntax_token literal), /* A literal nullptr with known type information. */
    /* A literal nullptr with known but invalid type information. The same functionality as bad_typed_literal_integer for nullptrs. */
    bad_typed_literal_nullptr(syntax_token literal),
 }

/* ===== Symbols =====
 * 
 * Note that while symbols are contained in symbol tables and scopes, this is not a requirement for the entire compilation process.
 * The use of symbol scopes is to provide lexical scoping while looking up symbols in the first phases of compilation.
 * The use of symbol tables is to store all exported symbols when compiling into and against libraries.
 *
 */

struct symbol_table
{
    symbol[dynamic] symbols;
}

struct symbol_scope
{
    symbol_scope *parent;
    symbol[dynamic] symbols;
}

struct symbol
{
    string name;
    /* We store the mangled name on symbols because of Laye's less strict identifier rules than other languages & namespacing.
     * If the name doesn't require mangling, it will be copied here.
     * 
     * This will likely also be used for imported symbols imported with a different name like what you'd see in C#:
     *
     *   [DllImport("addfn")] int AddFunction(int a, int b);
     *
     * where we refer to AddFunction in our code, but it's called addfn in the imported code.
     * In this case, symbol.name will be AddFunction and symbol.mangledName will be addfn.
     *
     * This may be overengineering this one field, but I think it's worth it with how simple it is.
     */
    string mangledName;

    symbol_kind kind;
    symbol_visibility_kind visibility;

    symbol_type_ref typeRef;
}

enum symbol_kind
{
    global_variable,
    global_function,

    local_variable,
    local_function,

    struct_field,
    union_field,

    function_parameter,
}

enum symbol_visibility_kind
{
    local, // <function local>
    exported, // laye's `public`
    project, // laye's `internal`
    file, // laye's `private`
}

struct symbol_type_table
{
    symbol_type[dynamic] types;
}

struct symbol_type_ref
{
    uint typeIndex; /* Opaque index into a symbol_type_table. The current implmenentation subtracts 1 from this index to look up the type. */
}

struct symbol_type
{
    string typeName;
    symbol_type_kind kind;
}

enum symbol_type_kind
{
    /* A generic result for expressions which have already had errors reported or for which no other bad type is valid.
     * Many invalid situations regarding types can still have sensible "poison" values.
     * When you would generate an error related to a poison type, instead you don't. */
    poison_type,
}

bool symbol_type_ref_is_valid(symbol_type_table table, symbol_type_ref typeRef)
{
    return typeRef.typeIndex != 0 and typeRef.typeIndex - 1 < table.types.length;
}

symbol_type symbol_type_table_lookup(symbol_type_table table, symbol_type_ref typeRef)
{
    if (not symbol_type_ref_is_valid(table, typeRef))
    {
        symbol_type invalidType;
        return invalidType;
    }

    uint index = typeRef.typeIndex - 1;
    return table.types[index];
}
