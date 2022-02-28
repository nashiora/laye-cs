struct diagnostic
{
	string message;
	source_span sourceSpan;
	diagnostic_kind kind;
}

enum diagnostic_kind
{
	info, warning, error
}

struct diagnostic_bag
{
	diagnostic[dynamic] diagnostics;
}

void diagnostics_add_info(diagnostic_bag *b, source_span sourceSpan, string message)
{
	diagnostic d;
	d.message = message;
	d.sourceSpan = sourceSpan;
	d.kind = ::info;
	dynamic_append(b.diagnostics, d);
}

void diagnostics_add_warning(diagnostic_bag *b, source_span sourceSpan, string message)
{
	diagnostic d;
	d.message = message;
	d.sourceSpan = sourceSpan;
	d.kind = ::warning;
	dynamic_append(b.diagnostics, d);
}

void diagnostics_add_error(diagnostic_bag *b, source_span sourceSpan, string message)
{
	diagnostic d;
	d.message = message;
	d.sourceSpan = sourceSpan;
	d.kind = ::error;
	dynamic_append(b.diagnostics, d);
}

// Laye tokens

struct laye_trivia
{
	source_span sourceSpan;

	laye_trivia_kind kind;
}

struct laye_token
{
	source_span sourceSpan;

	laye_trivia[] leadingTrivia;
	laye_trivia[] trailingTrivia;

	laye_token_kind kind;
}

enum laye_trivia_kind
{
	invalid,
	comment_line,
	comment_block,
	white_space,
}

enum laye_token_kind
{
	// likely no need for an `invalid` state or similar, we could use enum::nil?
	// we want 0 to be initialized in Laye so nil and null aren't ideal to have all over the place,
	// but how do we make it easy for the programmer to default enums?
	// obviously the first case is the 0 case, but that limits your ability to organize your code to look cleaner.
	// enum::nil is a sensible default since you have to either switch on an enum or explicitly cast to it, right?

	poison_token,

	eof,

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

	comma,
	dot,
	question,

	colon,
	semi_colon,

	colon_colon,
	equal_greater,
	question_dot,

	/* operator tokens */
	equal,

	plus, minus,
	star, slash,
	percent,

	amp, pipe, tilde,

	equal_equal, bang_equal,
	less, greater,
	less_equal, greater_equal,
	bang_less, bang_greater,
	bang_less_equal, bang_greater_equal,

	less_less, greater_greater,

	/* keyword tokens */
	kw_and, kw_or,
	kw_xor, kw_not,

	kw_cast, kw_is, // NOTE(local): `is` will probably be supported to have expressions like in C# so that nilable types don't require a comparison/cast or switch
	kw_sizeof, kw_offsetof,
	kw_nameof, kw_nameof_variant, // NOTE(local): `nameof_variant` is a holdover from the bootstrap compiler where I don't yet have a solution to this. At least this can be grepped and replaced in the future instead of a special cast rule : )
	kw_dynamic_append,

	kw_void,
	kw_int, kw_uint,
	kw_int_sized(u16 size), kw_uint_sized(u16 size),
	kw_bool, kw_float,
	kw_bool_sized(u16 size), kw_float_sized(u16 size),
	kw_rune, kw_string,
	kw_rawptr, kw_dynamic,
	kw_noreturn, kw_varargs,
	kw_struct, kw_enum,

	kw_const,
	kw_readonly, kw_writeonly,

	kw_true, kw_false,
	kw_nil, kw_nullptr,
	kw_context, kw_noinit,

	kw_global,

	kw_extern,
	kw_public, kw_private,
	// kw_internal, // internal by default, so no keyword for it?

	/* NOTE(local): use callconv(::variant) instead? easier to add new calling conventions without keywords */
	kw_callconv,
	//kw_cdecl, kw_nocontext,
	//kw_stdcall, kw_fastcall,
	//kw_naked, /* removes any compiler-generated functionality to the function */
	// TODO(local): are there any functions we specifically need to be intrinsics AND defined in the language?
	//kw_intrinsic,
	// TODO(local): how do we want to handle DLLs? is `export` the only thing or are there other cases that can remove keywords?
	//kw_export,
	kw_inline,

	kw_if, kw_else,
	kw_while, kw_for,
	kw_switch, kw_case,
	//kw_default, /* do we need `default`? `case:` serves the same purpose, and we don't need `= default` since we want everything to be default if set to 0 */
	kw_return, kw_yield, /* yield is probably only necessary when we get generators so can likely be ignored for a while. good to reserve it, though */
	kw_break, kw_continue,
}
