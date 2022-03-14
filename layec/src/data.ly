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
    syntax(syntax_node *node),
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
    if (d.sourceKind is ::syntax syntaxKind) return syntaxKind.node.sourceSpan;
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

    laye_identifier(string image), // the image should be the same as `source_span_to_string(token.sourceSpan)`, but we don't want to recompute that all the time

    // NOTE(local): a literal doesn't necessarily need to contain the value itself, we can re-parse it before code generation. see if you want to change this
    literal_integer(u64 value),
    literal_float(float value),
    literal_character(i32 value),
    literal_string(string value),
    literal_rune(i32 value),

    literal_string_unfinished,
    literal_rune_unfinished,

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

/* This does not contain a symbol_type_ref because it will be transformed into a symbol later */
struct binding_data
{
    syntax_node *[] modifiers;

    syntax_node *typeNode;
    syntax_token name;

    syntax_token tkAssignment;
    syntax_node *value;
}

binding_data *binding_data_alloc()
{
    binding_data *result = cast(binding_data *) malloc(sizeof(binding_data));
    
    binding_data zeroInit;
    *result = zeroInit;
    
    return result;
}

void binding_data_free(binding_data *node)
{
    free(cast(rawptr) node);
}

struct syntax_node
{
    source_span sourceSpan;

    syntax_node *[] annotations;
    syntax_node *[] modifiers;

    symbol_type_ref typeRef; /* typeRef will be invalid for non-expression nodes or nodes with no yet-resolved type. */

    syntax_node_kind kind;
}

syntax_node *syntax_node_alloc()
{
    syntax_node *result = cast(syntax_node *) malloc(sizeof(syntax_node));
    
    syntax_node zeroInit;
    *result = zeroInit;
    
    return result;
}

void syntax_node_free(syntax_node *node)
{
    free(cast(rawptr) node);
}

/* Many of the variants of syntax_node_kind are erroneous and only exist to provide better error message
 *   and attempt to continue parsing and checking the program and maybe provide use to a language server in the future.
 */
enum syntax_node_kind
{
    // ===== Identifier Nodes

    /* Simply represents the `global` keyword. */
    identifier_global(syntax_token identifier),

    /* a::b:c.d */
    /* Note that it can optionally have invalid separators if the parser thinks they're mistakes. */
    namespace_path(syntax_node *[] identifiers, syntax_token[] separators),
    /* The parser wanted a path, no path-related tokens were found */
    namespace_path_empty,

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

    literal_context(syntax_token literal),

    // ===== Top Level nodes

    /* #[if target.os == ::windows] */
    annotation_conditional( syntax_token tkOpen
                          , syntax_token tkIf
                          , syntax_node *condition
                          , syntax_token tkClose),

    /* #[program_entry] */
    annotation_identifier( syntax_token tkOpen
                         , syntax_token identifier
                         , syntax_token tkClose),

    /* #[ */
    annotation_only_open(syntax_token tkOpen),
    /* #[] */
    annotation_empty( syntax_token tkOpen
                    , syntax_token tkClose),
    /* #[namespace laye; */
    annotation_invalid_unclosed( syntax_token tkOpen
                               , syntax_token[] unparsedTokens),
    /* #[1 + 2] */
    annotation_invalid_closed( syntax_token tkOpen
                             , syntax_token[] unparsedTokens
                             , syntax_token tkClose),

    annotation_identifier_unclosed( syntax_token tkOpen
                                  , syntax_token identifier),

    /* using laye::io; */
    using_namespace( syntax_token tkUsing
                   , syntax_node *path
                   , syntax_token tkSemiColon),

    /* using laye::io */
    using_namespace_unfinished( syntax_token tkUsing
                              , syntax_node *path),
    /* using; */
    using_namespace_empty( syntax_token tkUsing
                         , syntax_token tkSemiColon),

    /* namespace laye::io; */
    namespace_unscoped( syntax_token tkNamespace
                      , syntax_node *path
                      , syntax_token tkSemiColon),

    /* namespace; */
    namespace_unscoped_empty( syntax_token tkNamespace
                            , syntax_token tkSemiColon),
    /* namespace laye::io */
    namespace_unscoped_unfinished( syntax_token tkNamespace
                                 , syntax_node *path),

    /* namespace laye::io { ... } */
    namespace_scoped( syntax_token tkNamespace
                    , syntax_node *path
                    , syntax_token tkOpenScope
                    , syntax_node *[] nodes
                    , syntax_token tkCloseScope),

    /* namespace laye::io { ... */
    namespace_scoped_unfinished( syntax_token tkNamespace
                               , syntax_node *path
                               , syntax_token tkOpenScope
                               , syntax_node *[] nodes),

    binding_declaration( binding_data *data
                       , syntax_token tkSemiColon),

    binding_declaration_and_assignment( binding_data *data
                                      , syntax_token tkAssign
                                      , syntax_node *value
                                      , syntax_token tkSemiColon),

    binding_declaration_and_assignment_unfinished( binding_data *data
                                                 , syntax_token tkAssign
                                                 , syntax_node *value),

    binding_declaration_unfinished(binding_data *data),

    function_declaration( syntax_node *type
                        , syntax_token name
                        , syntax_token tkOpenParams
                        , binding_data *[] parameters
                        , syntax_token[] tkParameterDelims
                        , syntax_token tkCloseParams
                        , syntax_node *body),

    function_declaration_unfinished( syntax_node *type
                                   , syntax_token name
                                   , syntax_token tkOpenParams),

    // ===== Modifiers

    modifier_const(syntax_token tkConst),
    modifier_inline(syntax_token tkInline),
    modifier_readonly(syntax_token tkReadOnly),
    modifier_writeonly(syntax_token tkWriteOnly),
    modifier_public(syntax_token tkPublic),
    modifier_private(syntax_token tkPrivate),
    modifier_foreign(syntax_token tkForeign),
    modifier_foreign_named(syntax_token tkForeign, syntax_token name),

    /* callconv(cdecl) */
    modifier_callconv_identifier( syntax_token tkCallConv
                                , syntax_token tkOpenCallConv
                                , syntax_token identifier
                                , syntax_token tkCloseCallConv),

    /* callconv(::cdecl) */
    modifier_callconv_infer_variant( syntax_token tkCallConv
                                   , syntax_token tkOpenCallConv
                                   , syntax_token tkDelimiter
                                   , syntax_token identifier
                                   , syntax_token tkCloseCallConv),

    /* callconv(calling_convention::cdecl) */
    modifier_callconv_variant( syntax_token tkCallConv
                             , syntax_token tkOpenCallConv
                             , syntax_token variantName
                             , syntax_token tkDelimiter
                             , syntax_token identifier
                             , syntax_token tkCloseCallConv),

    /* callconv */
    modifier_callconv_keyword_only(syntax_token tkCallConv),

    /* callconv() */
    modifier_callconv_empty( syntax_token tkCallConv
                           , syntax_token tkOpenCallConv
                           , syntax_token tkCloseCallConv),

    /* callconv(cdecl */
    modifier_callconv_unfinished( syntax_token tkCallConv
                                , syntax_token tkOpenCallConv
                                , syntax_token identifier),

    /* callconv( */
    modifier_callconv_unfinished_noname( syntax_token tkCallConv
                                       , syntax_token tkOpenCallConv),

    /* callconv(expression) */
    /* If any of the correct constructions of calling convention fail, it rewinds and parses any expression for the sake of having a node */
    modifier_callconv_expression( syntax_token tkCallConv
                                , syntax_token tkOpenCallConv
                                , syntax_node *expression
                                , syntax_token tkCloseCallConv),

    /* If the name of a calling convention variant is encountered in a modifier position and it can be guaranteed that it's intended to be
     *   used as a modifier, then this node stores that error case. */
    modifier_callconv_name_only(syntax_token tkCallConvName),

    // ===== Statements

    statement_expression(syntax_node *expression, syntax_token tkSemiColon),
    statement_arrow_expression(syntax_token tkArrow, syntax_node *expression, syntax_token tkSemiColon),

    statement_expression_unfinished(syntax_node *expression),
    statement_arrow_expression_unfinished(syntax_token tkArrow, syntax_node *expression),

    statement_empty(syntax_token tkSemiColon),
    statement_empty_unfinished,

    statement_block(syntax_token tkOpenBlock, syntax_node *[] nodes, syntax_token tkCloseBlock),
    statement_block_unfinished(syntax_token tkOpenBlock, syntax_node *[] nodes),

    statement_if( syntax_token tkIf
                , syntax_token tkOpenCondition
                , syntax_node *condition
                , syntax_token tkCloseCondition
                , syntax_node *passBody),

    statement_return(syntax_token tkReturn, syntax_token tkSemiColon),

    // ===== Primary Expressions

    expression_eof,
    expression_missing,

    expression_unknown_token(syntax_token token),

    /* This is not an error during parsing. Represents an identifier with no yet-known meaning.
     * If this node cannot be replaced with a resolved identifier node during checking then an unresolved identifier error is issued. */
    expression_identifier_unresolved(syntax_token identifier),
    expression_identifier_global(syntax_token identifier),

    /* global::win32::INVALID_HANDLE_VALUE */
    /* Note that this is just arbitrary syntax in the same form as a namespace path.
     * This can later be resolved to be part namespace path and part symbol lookup inside that
     *   namespace or even a union variant constructor. */
    expression_lookup(syntax_node *path),

    expression_static_named_index( syntax_node *target
                                 , syntax_token tkDot
                                 , syntax_token fieldName),

    expression_static_named_index_unfinished( syntax_node *target , syntax_token tkDot),

    expression_invoke( syntax_node *target
                     , syntax_token tkOpenParen
                     , syntax_node *[] arguments
                     , syntax_token[] tkDelimiters
                     , syntax_token tkCloseParen),

    expression_invoke_unfinished( syntax_node *target
                                , syntax_token tkOpenParen
                                , syntax_node *[] arguments
                                , syntax_token[] tkDelimiters),

    expression_grouped(syntax_token tkOpen, syntax_node *expression, syntax_token tkClose),
    expression_grouped_unfinished(syntax_token tkOpen, syntax_node *expression),

    expression_logical_not(syntax_token tkNot, syntax_node *target),
    expression_negate(syntax_token tkMinus, syntax_node *target),
    expression_complement(syntax_token tkTilde, syntax_node *target),
    expression_address_of(syntax_token tkAmp, syntax_node *target),
    expression_dereference(syntax_token tkStar, syntax_node *target),

    expression_explicit_cast( syntax_token tkCast
                            , syntax_token tkOpenType
                            , syntax_node *typeNode
                            , syntax_token tkCloseType
                            , syntax_node *expression),

    expression_explicit_cast_unfinished( syntax_token tkCast
                                       , syntax_token tkOpen
                                       , syntax_node *typeNode
                                       , syntax_node *expression),

    expression_explicit_cast_missing_type( syntax_token tkCast
                                         , syntax_node *expression),

    // ===== Compound Expressions

    expression_logical_and(syntax_node *left, syntax_token tkOperator, syntax_node *right),
    expression_logical_or(syntax_node *left, syntax_token tkOperator, syntax_node *right),
    expression_logical_xor(syntax_node *left, syntax_token tkOperator, syntax_node *right),

    expression_compare_equal(syntax_node *left, syntax_token tkOperator, syntax_node *right),

    // ===== Types

    type_var(syntax_token tkVar),
    type_void(syntax_token tkVoid),
    type_bool(syntax_token tkBool),
    type_bool_sized(syntax_token tkBool, u16 size),
    type_int(syntax_token tkInt),
    type_int_sized(syntax_token tkInt, u16 size),
    type_uint(syntax_token tkUInt),
    type_uint_sized(syntax_token tkUInt, u16 size),
    type_float(syntax_token tkFloat),
    type_float_sized(syntax_token tkFloat, u16 size),
    type_string(syntax_token tkString),
    type_rune(syntax_token tkRune),
    type_rawptr(syntax_token tkRawptr),
    type_noreturn(syntax_token tkNoreturn),

    type_named(syntax_node *path),

    type_nilable(syntax_node *typeNode, syntax_token tkQuestion),
    /* Note that only pointers and buffers can be compared to `nullptr`.
     * They also still receive the same pattern matching and monadic nature of nilable types. */ 
    type_nullable(syntax_node *typeNode, syntax_token tkQuestion),

    type_pointer(syntax_node *elementTypeNode, syntax_node *[] modifiers, syntax_token tkPointer),
    
    type_buffer( syntax_node *elementTypeNode
               , syntax_node *[] modifiers
               , syntax_token tkOpenBuffer
               , syntax_token tkBuffer
               , syntax_token tkCloseBuffer),

    type_buffer_unfinished( syntax_node *elementTypeNode
                          , syntax_node *[] modifiers
                          , syntax_token tkOpenBuffer
                          , syntax_token tkBuffer),

    type_slice( syntax_node *elementTypeNode
              , syntax_node *[] modifiers
              , syntax_token tkOpenSlice
              , syntax_token tkCloseSlice),
    
    type_list( syntax_node *elementTypeNode
             , syntax_node *[] modifiers
             , syntax_token tkOpenList
             , syntax_token tkDynamic
             , syntax_token tkCloseList),

    type_ambiguous_container( syntax_node *elementTypeNode
                            , syntax_node *[] modifiers
                            , syntax_token tkOpenArray
                            , syntax_node *[] anyNodes
                            , syntax_token tkCloseArray),

    type_array( syntax_node *elementTypeNode
              , syntax_node *[] modifiers
              , syntax_token tkOpenArray
              , syntax_node *[] arrayLengths
              , syntax_token tkCloseArray),

    type_dictionary( syntax_node *elementTypeNode
                   , syntax_node *[] modifiers
                   , syntax_token tkOpenArray
                   , syntax_node *[] indexTypeNodes
                   , syntax_token tkCloseArray),

    type_tuple( syntax_token tkOpen
              , binding_data *[] fields
              , syntax_token[] delimiters
              , syntax_token tkClose),

    type_empty,
    type_dangling_modifiers(syntax_node *elementTypeNode, syntax_node *[] modifiers),
}

bool syntax_node_type_contains_var(syntax_node *typeNode)
{
    if (typeNode.kind is ::type_var)
        return true;
    else if (typeNode.kind is ::type_pointer typePointer)
        return syntax_node_type_contains_var(typePointer.elementTypeNode);
    else if (typeNode.kind is ::type_buffer typeBuffer)
        return syntax_node_type_contains_var(typeBuffer.elementTypeNode);
    else return false;
}

bool syntax_node_is_type(syntax_node *node)
{
    // TODO(local): don't use string compares to check if it's a type
    return string_start_equals("type_", nameof_variant(node.kind), 5);
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
