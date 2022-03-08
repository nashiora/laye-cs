bool rune_is_laye_digit(i32 r)
{
	return r < 127 and char_is_digit(cast(u8) r);
}

bool rune_is_laye_digit_in_radix(i32 r, uint radix)
{
	uint decimalDigitMax = radix - 1;
	if (radix >= 10) decimalDigitMax = 9;

	if (r >= 48 /* 0 */ and r <= (decimalDigitMax + 48 /* 0 */))
		return true;

	uint letterDigitMax = radix - 10 - 1;
	if (r >= 65 /* A */ and r <= (letterDigitMax + 65 /* A */))
		return true;

	if (r >= 97 /* a */ and r <= (letterDigitMax + 97 /* a */))
		return true;

	return false;
}

uint rune_laye_digit_value(i32 r)
{
	return cast(uint) (r - 48 /* 0 */);
}

uint rune_laye_digit_value_in_radix(i32 r, uint radix)
{
	uint decimalDigitMax = radix - 1;
	if (radix >= 10) decimalDigitMax = 9;

	if (r >= 48 /* 0 */ and r <= (decimalDigitMax + 48 /* 0 */))
		return cast(uint) (r - 48 /* 0 */);

	uint letterDigitMax = radix - 10 - 1;
	if (r >= 65 /* A */ and r <= (letterDigitMax + 65 /* A */))
		return 10 + cast(uint) (r - 65 /* A */);

	if (r >= 97 /* a */ and r <= (letterDigitMax + 97 /* a */))
		return 10 + cast(uint) (r - 97 /* a */);

	return 0;
}

bool rune_is_laye_identifier_part(i32 r)
{
	return unicode_is_digit(r) or unicode_is_letter(r) or r == 95 /* `_` */;
}

syntax_trivia[] lexer_get_syntax_trivia(lexer_data *l, bool untilEndOfLine)
{
	syntax_trivia[dynamic] triviaList;

	while (not lexer_is_eof(*l))
	{
		syntax_trivia trivia;
		source_location startLocation = lexer_current_location(*l);

		i32 c = lexer_current_rune(*l);

		if (unicode_is_white_space(c))
		{
			while (not lexer_is_eof(*l) and unicode_is_white_space(lexer_current_rune(*l)))
			{
				c = lexer_current_rune(*l);
				lexer_advance(l);

				if (c == 10 and untilEndOfLine)
					break;
			}

			source_location endLocation = lexer_current_location(*l);

			trivia.kind = ::white_space;
			trivia.sourceSpan = source_span_create(startLocation, endLocation);
		}
		else if (c == 47 /* `/` */ and lexer_peek_rune(*l) == 47 /* `/` */)
		{
			while (not lexer_is_eof(*l))
			{
				c = lexer_current_rune(*l);
				lexer_advance(l);

				if (c == 10) break;
			}

			source_location endLocation = lexer_current_location(*l);

			trivia.kind = ::comment_line;
			trivia.sourceSpan = source_span_create(startLocation, endLocation);
		}
		else if (c == 47 /* `/` */ and lexer_peek_rune(*l) == 42 /* `*` */)
		{
			lexer_advance(l); // `/`
			lexer_advance(l); // `*`

			uint nesting = 1;

			while (not lexer_is_eof(*l))
			{
				c = lexer_current_rune(*l);
				lexer_advance(l);

				if (c == 47 /* `/` */ and lexer_current_rune(*l) == 42 /* `*` */)
				{
					lexer_advance(l); // `*`
					nesting = nesting + 1;
				}
				else if (c == 42 /* `*` */ and lexer_current_rune(*l) == 47 /* `/` */)
				{
					lexer_advance(l); // `/`
					nesting = nesting - 1;

					if (nesting == 0) break;
				}
			}

			source_location endLocation = lexer_current_location(*l);
			trivia.sourceSpan = source_span_create(startLocation, endLocation);

			if (nesting > 0)
				diagnostics_add_error(l.diagnostics, ::trivia(trivia), "unfinished block comment");
			else trivia.kind = ::comment_block;
		}
		else break;

		dynamic_append(triviaList, trivia);
		//if (trivia.kind == syntax_trivia_kind::invalid) break;
	}

	syntax_trivia[] trivia = triviaList[:triviaList.length];
	dynamic_free(triviaList);
	return trivia;
}

syntax_token lexer_read_laye_token(lexer_data *l)
{
	assert(not lexer_is_eof(*l), "lexer_read_syntax_token called when at EoF");

	syntax_token token;

	syntax_trivia[] leadingTrivia = lexer_get_syntax_trivia(l, false);
	token.leadingTrivia = leadingTrivia;

	i32 c = lexer_current_rune(*l);

	source_location startLocation = lexer_current_location(*l);
	if (c == 0)
	{
		lexer_advance(l); // EoF, calling function needs to know we consumed a character pepehands
		token.kind = ::eof;
		token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));
		return token;
	}

	if (rune_is_laye_identifier_part(c))
		lexer_read_laye_identifier_or_number(l, &token);
	// TODO(local): switch statements
	else if (c == 33 /* ! */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 60 /* < */)
		{
			lexer_advance(l);
			token.kind = ::bang_less;
		}
		else if (lexer_current_rune(*l) == 61 /* = */)
		{
			lexer_advance(l);
			token.kind = ::bang_equal;
		}
		else if (lexer_current_rune(*l) == 62 /* > */)
		{
			lexer_advance(l);
			token.kind = ::bang_greater;
		}
		else
		{
			token.kind = ::poison_token;
			// set the source span here, because we're copying it into the diagnostic
			token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));

			diagnostics_add_error(l.diagnostics, ::token(token), "invalid token `!` in Laye source text (did you mean `not`?)");
		}
	}
	else if (c == 34 /* " */)
		lexer_read_laye_string(l, &token);
	else if (c == 35 /* # */)
	{
		lexer_advance(l);
		if (c == 91 /* [ */)
		{
			lexer_advance(l);
			token.kind = ::hash_open_bracket;
		}
		else
		{
			token.kind = ::poison_token;
			// set the source span here, because we're copying it into the diagnostic
			token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));

			diagnostics_add_error(l.diagnostics, ::token(token), "invalid token `#` in Laye source text (did you mean `#[`?)");
		}
	}
	else if (c == 37 /* % */)
	{
		lexer_advance(l);
		token.kind = ::percent;
	}
	else if (c == 38 /* & */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 38 /* & */)
		{
			lexer_advance(l);
			token.kind = ::poison_token;
			// set the source span here, because we're copying it into the diagnostic
			token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));

			diagnostics_add_warning(l.diagnostics, ::token(token), "suspicious token `&&` in Laye source text (did you mean `and`?)");
			diagnostics_add_info(l.diagnostics, ::token(token), "`&` can be either the bitwise and infix operator or the address-of prefix operator; `&&` could only legally appear as taking an address of an address inline, which is incredibly unlikely and must instead be written as `&(&variable)` if needed to avoid the much more common mistake of using `&&` as the logical and infix operator");
		}
		else token.kind = ::amp;
	}
	else if (c == 40 /* ( */)
	{
		lexer_advance(l);
		token.kind = ::open_paren;
	}
	else if (c == 41 /* ) */)
	{
		lexer_advance(l);
		token.kind = ::close_paren;
	}
	else if (c == 42 /* * */)
	{
		lexer_advance(l);
		token.kind = ::star;
	}
	else if (c == 43 /* + */)
	{
		lexer_advance(l);
		token.kind = ::plus;
	}
	else if (c == 44 /* , */)
	{
		lexer_advance(l);
		token.kind = ::comma;
	}
	else if (c == 45 /* - */)
	{
		lexer_advance(l);
		token.kind = ::minus;
	}
	else if (c == 46 /* . */)
	{
		lexer_advance(l);
		token.kind = ::dot;
	}
	else if (c == 47 /* / */)
	{
		lexer_advance(l);
		token.kind = ::slash;
	}
	else if (c == 58 /* : */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 58 /* : */)
		{
			lexer_advance(l);
			token.kind = ::colon_colon;
		}
		else token.kind = ::colon;
	}
	else if (c == 59 /* ; */)
	{
		lexer_advance(l);
		token.kind = ::semi_colon;
	}
	else if (c == 60 /* < */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 60 /* < */)
		{
			lexer_advance(l);
			token.kind = ::less_less;
		}
		else if (lexer_current_rune(*l) == 61 /* = */)
		{
			lexer_advance(l);
			token.kind = ::less_equal;
		}
		else token.kind = ::less;
	}
	else if (c == 61 /* = */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 61 /* = */)
		{
			lexer_advance(l);
			token.kind = ::equal_equal;
		}
		else token.kind = ::equal;
	}
	else if (c == 62 /* > */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 62 /* > */)
		{
			lexer_advance(l);
			token.kind = ::greater_greater;
		}
		else if (lexer_current_rune(*l) == 61 /* = */)
		{
			lexer_advance(l);
			token.kind = ::greater_equal;
		}
		else token.kind = ::greater;
	}
	else if (c == 91 /* [ */)
	{
		lexer_advance(l);
		token.kind = ::open_bracket;
	}
	else if (c == 93 /* ] */)
	{
		lexer_advance(l);
		token.kind = ::close_bracket;
	}
	else if (c == 123 /* { */)
	{
		lexer_advance(l);
		token.kind = ::open_brace;
	}
	else if (c == 124 /* | */)
	{
		lexer_advance(l);
		if (lexer_current_rune(*l) == 124 /* | */)
		{
			lexer_advance(l);
			token.kind = ::poison_token;

			source_span ss = source_span_create(startLocation, lexer_current_location(*l));

			diagnostics_add_warning(l.diagnostics, ::span(ss), "invalid token `||` (did you mean `or`?)");
			diagnostics_add_info(l.diagnostics, ::span(ss), "`|` is the bitwise or infix operator; `||` could never legally appear in an expression");
		}
		else token.kind = ::pipe;
	}
	else if (c == 125 /* } */)
	{
		lexer_advance(l);
		token.kind = ::close_brace;
	}
	else if (c == 126 /* ~ */)
	{
		lexer_advance(l);
		token.kind = ::tilde;
	}

	token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));

	if (token.kind == nil)
	{
		lexer_advance(l);

		string_builder errorBuilder;
		string_builder_append_string(&errorBuilder, "unrecognized character `");
		string_builder_append_rune(&errorBuilder, c);
		string_builder_append_string(&errorBuilder, "` (U+");
		string_builder_append_uint_hexn(&errorBuilder, cast(uint) c, 4);
		string_builder_append_string(&errorBuilder, ") in lexer");

		diagnostics_add_error(l.diagnostics, ::token(token), string_builder_to_string(errorBuilder));

		string_builder_free(&errorBuilder);
	}

	// any diagnostics generated before this point will not have trailing trivia, which is a bit said but totally usable.
	// we'd have to heap-allocate each token, which we may do in the future if we can custom-allocate into a big arena, for example.
	// i'd still want to avoid the extra pointer walk, but if we need more reference-like semantics in the lexer in the future it's not a terible idea

	syntax_trivia[] trailingTrivia = lexer_get_syntax_trivia(l, true);
	token.trailingTrivia = trailingTrivia;

	if (true)
	{
        string tokenLocationString = source_location_to_string(token.sourceSpan.startLocation);
        string tokenString = source_span_to_string(token.sourceSpan);
        string tokenKindString = nameof_variant(token.kind);

        printf("> token %.*s (%.*s) %.*s%c", tokenKindString.length, tokenKindString.data,
            tokenLocationString.length, tokenLocationString.data,
            tokenString.length, tokenString.data,
            10);
	}

	return token;
}

void lexer_read_laye_identifier_or_number(lexer_data *l, syntax_token *token)
{
	assert(rune_is_laye_identifier_part(lexer_current_rune(*l)), "lexer_read_syntax_token called without identifier char");

	i32 lastRune = lexer_current_rune(*l);

	bool isStillNumber = lastRune == 95 /* _ */ or rune_is_laye_digit(lastRune);
	bool doesNumberHaveInvalidUnderscorePlacement = lastRune == 95 /* _ */;

	u64 currentIntegerValue = 0;

	source_location startLocation = lexer_current_location(*l);
	while (not lexer_is_eof(*l) and rune_is_laye_identifier_part(lexer_current_rune(*l)))
	{
		lastRune = lexer_current_rune(*l);
		lexer_advance(l); // ident char

		if (isStillNumber)
		{
			isStillNumber = lastRune == 95 /* _ */ or rune_is_laye_digit(lastRune);
			if (isStillNumber and lastRune != 95 /* _ */)
			{
				currentIntegerValue = currentIntegerValue * 10;
				currentIntegerValue = currentIntegerValue + cast(u64) rune_laye_digit_value(lastRune);
			}
		}
	}

	if (isStillNumber)
	{
		// If the number started or ended with an underscore, it's not a valid number and we should report it as an error
		doesNumberHaveInvalidUnderscorePlacement = lastRune == 95 /* _ */;

		if (lexer_current_rune(*l) == 35 /* # */)
			lexer_read_laye_radix_integer_from_delimiter(l, token, startLocation, currentIntegerValue);
		else
		{
			token.kind = ::literal_integer(currentIntegerValue);
		}
	}
	else
	{
		// the main lexer_read_syntax_token function will calculate the sourceSpan for us, but we need it here to get the identifier value

		source_location endLocation = lexer_current_location(*l);
		token.sourceSpan = source_span_create(startLocation, endLocation);

		string image = source_span_to_string(token.sourceSpan);
		token.kind = get_laye_keyword_kind(l, image);
	}
}

u16 is_image_suffixed_with_positive_integer(string image, uint startIndex)
{
	assert(image.length > 0, "is_image_suffixed_with_positive_integer passed an image with no characters");
	assert(startIndex > 0, "is_image_suffixed_with_positive_integer passed an index of 0");

	uint i = startIndex;
	uint suffix = 0;

	while (i < image.length)
	{
		// we don't need to use the rune functions here since the things we're checking for are all ASCII
		u8 c = image[i];
		if (not char_is_digit(c)) return 0;

		suffix = suffix * 10;
		suffix = suffix + c - 48;

		i = i + 1;
	}

	if (suffix > 65535) suffix = 0;
	return cast(u16) suffix;
}

syntax_token_kind get_laye_keyword_kind(lexer_data *l, string image)
{
	assert(image.length > 0, "get_laye_keyword_kind passed an image with no characters");

	// TODO(local): switch (or LUT)
	if (image[0] == 97 /* a */)
	{
		if (string_equals(image, "and"))
			return ::kw_and;
	}
	else if (image[0] == 98 /* b */)
	{
		if (string_equals(image, "bool"))
			return ::kw_bool;
		else if (string_equals(image, "break"))
			return ::kw_break;
		else
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 1);
			if (suffixValue != 0)
				return ::kw_bool_sized(suffixValue);
		}
	}
	else if (image[0] == 99 /* c */)
	{
		if (string_equals(image, "callconv"))
			return ::kw_callconv;
		else if (string_equals(image, "case"))
			return ::kw_case;
		else if (string_equals(image, "cast"))
			return ::kw_cast;
		else if (string_equals(image, "const"))
			return ::kw_const;
		else if (string_equals(image, "context"))
			return ::kw_context;
		else if (string_equals(image, "continue"))
			return ::kw_continue;
	}
	else if (image[0] == 100 /* d */)
	{
		/* if (string_equals(image, "default"))
			return ::kw_default;
		else */ if (string_equals(image, "defer"))
			return ::kw_defer;
		else if (string_equals(image, "dynamic"))
			return ::kw_dynamic;
		else if (string_equals(image, "dynamic_append"))
			return ::kw_dynamic_append;
		else if (string_equals(image, "dynamic_free"))
			return ::kw_dynamic_append;
	}
	else if (image[0] == 101 /* e */)
	{
		// TODO(local): `export` ?
		if (string_equals(image, "else"))
			return ::kw_else;
		else if (string_equals(image, "enum"))
			return ::kw_enum;
		/* else if (string_equals(image, "extern"))
			return ::kw_extern; */
	}
	else if (image[0] == 102 /* f */)
	{
		if (string_equals(image, "false"))
			return ::kw_false;
		else if (string_equals(image, "float"))
			return ::kw_float;
		else if (string_equals(image, "for"))
			return ::kw_for;
		else if (string_equals(image, "foreign"))
			return ::kw_foreign;
		else
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 1);
			if (suffixValue != 0)
				return ::kw_float_sized(suffixValue);
		}
	}
	else if (image[0] == 103 /* g */)
	{
		if (string_equals(image, "global"))
			return ::kw_global;
	}
	else if (image[0] == 105 /* i */)
	{
		if (string_equals(image, "if"))
			return ::kw_if;
		else if (string_equals(image, "inline"))
			return ::kw_inline;
		else if (string_equals(image, "int"))
			return ::kw_int;
		else /* if (string_equals(image, "intrinsic"))
			return ::kw_intrinsic;
		else */ if (string_equals(image, "is"))
			return ::kw_is;
		else if (string_start_equals(image, "ileast", 6))
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 6);
			if (suffixValue != 0)
				return ::kw_int_least_sized(suffixValue);
		}
		else
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 1);
			if (suffixValue != 0)
				return ::kw_int_sized(suffixValue);
		}
	}
	else if (image[0] == 110 /* n */)
	{
		if (string_equals(image, "naked"))
			return ::kw_naked;
		else if (string_equals(image, "nameof"))
			return ::kw_nameof;
		else if (string_equals(image, "nameof_variant"))
			return ::kw_nameof_variant;
		else if (string_equals(image, "namespace"))
			return ::kw_namespace;
		else if (string_equals(image, "nil"))
			return ::kw_nil;
		else if (string_equals(image, "noinit"))
			return ::kw_noinit;
		else if (string_equals(image, "not"))
			return ::kw_not;
		else if (string_equals(image, "noreturn"))
			return ::kw_noreturn;
		else if (string_equals(image, "nullptr"))
			return ::kw_nullptr;
	}
	else if (image[0] == 111 /* o */)
	{
		if (string_equals(image, "offsetof"))
			return ::kw_offsetof;
		else if (string_equals(image, "or"))
			return ::kw_or;
	}
	else if (image[0] == 112 /* p */)
	{
		if (string_equals(image, "private"))
			return ::kw_private;
		else if (string_equals(image, "public"))
			return ::kw_public;
	}
	else if (image[0] == 114 /* r */)
	{
		if (string_equals(image, "rawptr"))
			return ::kw_rawptr;
		else if (string_equals(image, "readonly"))
			return ::kw_readonly;
		else if (string_equals(image, "return"))
			return ::kw_return;
		else if (string_equals(image, "rune"))
			return ::kw_rune;
	}
	else if (image[0] == 115 /* s */)
	{
		if (string_equals(image, "sizeof"))
			return ::kw_sizeof;
		else if (string_equals(image, "string"))
			return ::kw_string;
		else if (string_equals(image, "struct"))
			return ::kw_struct;
		else if (string_equals(image, "switch"))
			return ::kw_switch;
	}
	else if (image[0] == 116 /* t */)
	{
		if (string_equals(image, "true"))
			return ::kw_true;
		else if (l.contextLayeCompileTimeExpression and string_equals(image, "target"))
			return ::kw_target;
	}
	else if (image[0] == 117 /* u */)
	{
		if (string_equals(image, "uint"))
			return ::kw_uint;
		else if (string_equals(image, "unreachable"))
			return ::kw_unreachable;
		else if (string_equals(image, "using"))
			return ::kw_using;
		else if (string_start_equals(image, "uleast", 6))
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 6);
			if (suffixValue != 0)
				return ::kw_uint_least_sized(suffixValue);
		}
		else
		{
			u16 suffixValue = is_image_suffixed_with_positive_integer(image, 1);
			if (suffixValue != 0)
				return ::kw_uint_sized(suffixValue);
		}
	}
	else if (image[0] == 118 /* v */)
	{
		if (string_equals(image, "var"))
			return ::kw_var;
		else if (string_equals(image, "varargs"))
			return ::kw_varargs;
		else if (string_equals(image, "void"))
			return ::kw_void;
	}
	else if (image[0] == 119 /* w */)
	{
		if (string_equals(image, "while"))
			return ::kw_while;
		else if (string_equals(image, "writeonly"))
			return ::kw_writeonly;
	}
	else if (image[0] == 120 /* x */)
	{
		if (string_equals(image, "xor"))
			return ::kw_xor;
	}
	else if (image[0] == 121 /* y */)
	{
		if (string_equals(image, "yield"))
			return ::kw_yield;
	}

	return ::ident(image);
}

void lexer_read_laye_radix_integer_from_delimiter(lexer_data *l, syntax_token *token, source_location startLocation, u64 radix)
{
	panic("TODO(local): lexer_read_laye_radix_integer_from_delimiter is unimplemented");
}

i32 lexer_read_laye_escape_sequence_to_rune(lexer_data *l)
{
	source_location startLocation = lexer_current_location(*l);

	assert(lexer_current_rune(*l) == 92 /* \ */, "lexer_read_laye_escape_to_rune called without escape char");
	lexer_advance(l); // `\`

	i32 c = lexer_current_rune(*l);

	if (c == 85 /* U */)
	{
		lexer_advance(l); // `U`

		i32 codepointValue = 0;

		{
			uint i = 0;
			while (i < 6)
			{
				if (not rune_is_laye_digit_in_radix(lexer_current_rune(*l), 16))
				{
					source_span ss = source_span_create(startLocation, lexer_current_location(*l));
					diagnostics_add_error(l.diagnostics, ::span(ss), "expected 6 hex digits for full range unicode escape sequence");
					break;
				}

				codepointValue = codepointValue * 16;
				codepointValue = codepointValue + cast(i32) rune_laye_digit_value_in_radix(lexer_current_rune(*l), 16);

				lexer_advance(l); // base 16 digit

				i = i + 1;
			}
		}

		return codepointValue;
	}
	else if (c == 117 /* u */)
	{
		lexer_advance(l); // `u`

		i32 codepointValue = 0;

		{
			uint i = 0;
			while (i < 4)
			{
				if (not rune_is_laye_digit_in_radix(lexer_current_rune(*l), 16))
				{
					source_span ss = source_span_create(startLocation, lexer_current_location(*l));
					diagnostics_add_error(l.diagnostics, ::span(ss), "expected 4 hex digits for base range unicode escape sequence");
					break;
				}

				codepointValue = codepointValue * 16;
				codepointValue = codepointValue + cast(i32) rune_laye_digit_value_in_radix(lexer_current_rune(*l), 16);

				lexer_advance(l); // base 16 digit

				i = i + 1;
			}
		}

		return codepointValue;
	}
	else if (c == 120 /* x */)
	{
		lexer_advance(l); // `x`

		i32 codepointValue = 0;

		{
			uint i = 0;
			while (i < 2)
			{
				if (not rune_is_laye_digit_in_radix(lexer_current_rune(*l), 16))
				{
					source_span ss = source_span_create(startLocation, lexer_current_location(*l));
					diagnostics_add_error(l.diagnostics, ::span(ss), "expected 2 digits for hex escape sequence");
					break;
				}

				codepointValue = codepointValue * 16;
				codepointValue = codepointValue + cast(i32) rune_laye_digit_value_in_radix(lexer_current_rune(*l), 16);

				lexer_advance(l); // base 16 digit

				i = i + 1;
			}
		}

		return codepointValue;
	}
	else if (c == 97 /* a */)
	{
		lexer_advance(l); // `a`
		return 7 /* \a */;
	}
	else if (c == 98 /* b */)
	{
		lexer_advance(l); // `b`
		return 8 /* \b */;
	}
	else if (c == 116 /* t */)
	{
		lexer_advance(l); // `t`
		return 9 /* \t */;
	}
	else if (c == 110 /* n */)
	{
		lexer_advance(l); // `n`
		return 10 /* \n */;
	}
	else if (c == 118 /* v */)
	{
		lexer_advance(l); // `v`
		return 11 /* \v */;
	}
	else if (c == 102 /* f */)
	{
		lexer_advance(l); // `f`
		return 12 /* \f */;
	}
	else if (c == 114 /* r */)
	{
		lexer_advance(l); // `r`
		return 13 /* \r */;
	}
	else if (c == 34 /* " */)
	{
		lexer_advance(l); // `"`
		return 34 /* " */;
	}
	else if (c == 39 /* ' */)
	{
		lexer_advance(l); // `'`
		return 39 /* ' */;
	}
	else if (c == 92 /* \ */)
	{
		lexer_advance(l); // `\`
		return 92 /* \ */;
	}
	else
	{
		i32 result = lexer_current_rune(*l);

		lexer_advance(l); // next character, unrecognized
		// TODO(local): create a flag to treat unknown escapes as non-errors
		source_span ss = source_span_create(startLocation, lexer_current_location(*l));
		diagnostics_add_error(l.diagnostics, ::span(ss), "unrecognized escape sequence");

		return result;
	}
}

void lexer_read_laye_string(lexer_data *l, syntax_token *token)
{
	assert(lexer_current_rune(*l) == 34 /* " */, "lexer_read_laye_string called without quote char");
	lexer_advance(l); // `"`

	source_location startLocation = lexer_current_location(*l);

	string_builder sb;
	while (not lexer_is_eof(*l) and lexer_current_rune(*l) != 34 /* " */)
	{
		i32 c = lexer_current_rune(*l);

		if (c == 92 /* \ */)
		{
			i32 r = lexer_read_laye_escape_sequence_to_rune(l);
			string_builder_append_rune(&sb, r);
		}
		else
		{
			lexer_advance(l); // c
			string_builder_append_rune(&sb, c);	
		}
	}

	if (lexer_current_rune(*l) == 34 /* " */)
	{
		lexer_advance(l); // `"`
		string stringValue = string_builder_to_string(sb);
		token.kind = ::literal_string(stringValue);
		//printf(">> %.*s%c", stringValue.length, stringValue.data, 10);
		string_builder_free(&sb);
	}
	else
	{
		string locationString = source_location_to_string(startLocation);
		printf("%.*s: error: unfinished string literal%c", locationString.length, locationString.data, 10);
	}
}
