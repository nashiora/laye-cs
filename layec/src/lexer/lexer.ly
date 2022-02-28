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

struct lexer_data
{
	source currentSource;
	uint currentIndex;

	uint currentColumn;
	uint currentLine;

	diagnostic_bag *diagnostics;
}

bool lexer_is_eof(lexer_data l)
{
	return l.currentIndex >= l.currentSource.text.length or l.currentSource.text[l.currentIndex] == 0;
}

i32 lexer_current_rune(lexer_data l)
{
	if (lexer_is_eof(l)) return 0;
	//return cast(i32) l.currentSource.text[l.currentIndex];
	return unicode_utf8_string_rune_at_index(l.currentSource.text, l.currentIndex);
}

i32 lexer_peek_rune(lexer_data l)
{
	if (lexer_is_eof(l)) return 0;
	uint currentByteCount = unicode_utf8_calc_encoded_byte_count(l.currentSource.text[l.currentIndex]);

	//if (l.currentIndex + currentByteCount >= l.currentSource.text.length) return 0;
	//return cast(i32) l.currentSource.text[l.currentIndex + 1];
	return unicode_utf8_string_rune_at_index(l.currentSource.text, l.currentIndex + currentByteCount); // this function checks for bounds errors etc. and returns 0 if so
}

source_location lexer_current_location(lexer_data l)
{
	source_location location;
	location.source = l.currentSource;
	location.characterIndex = l.currentIndex;
	location.lineNumber = l.currentLine;
	location.columnNumber = l.currentColumn;
	return location;
}

void lexer_advance(lexer_data *l)
{
	if (lexer_is_eof(*l)) return;

	i32 c = lexer_current_rune(*l);
	l.currentIndex = l.currentIndex + 1;

	if (c == 10)
	{
		l.currentLine = l.currentLine + 1;
		l.currentColumn = 1;
	}
	else l.currentColumn = l.currentColumn + 1;
}

laye_trivia[] lexer_get_laye_trivia(lexer_data *l, bool untilEndOfLine)
{
	laye_trivia[dynamic] triviaList;

	while (not lexer_is_eof(*l))
	{
		laye_trivia trivia;
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
				diagnostics_add_error(l.diagnostics, source_span_create(startLocation, endLocation), "unfinished block comment");
			else trivia.kind = ::comment_block;
		}
		else break;

		dynamic_append(triviaList, trivia);
		//if (trivia.kind == laye_trivia_kind::invalid) break;
	}

	laye_trivia[] trivia = triviaList[:triviaList.length];
	dynamic_free(triviaList);
	return trivia;
}

laye_token[] lexer_read_laye_tokens(source source, diagnostic_bag *diagnostics)
{
	if (not source.isValid)
	{
		laye_token[] dummyResult;
		return dummyResult;
	}

	laye_token[dynamic] resultTokens;

	lexer_data l;
	l.diagnostics = diagnostics;
	l.currentSource = source;
	l.currentLine = 1;
	l.currentColumn = 1;

	while (not lexer_is_eof(l))
	{
		//printf("  reading token...%c", 10);

		uint currentIndex = l.currentIndex;
		laye_token token = lexer_read_laye_token(&l);

		// if we didn't advance, panic
		assert(currentIndex != l.currentIndex, "internal Laye lexer error: call to `lexer_read_laye_token` did not consume any characters");
		assert(token.kind != nil, "internal Laye lexer error: call to `lexer_read_laye_token` returned a nil-kinded token");

		// TODO(local): check if the token is EoF token? don't append? EoF can have trivia that needs to be maintained somehow
		dynamic_append(resultTokens, token);
	}

	laye_token[] tokens = resultTokens[:resultTokens.length];
	dynamic_free(resultTokens);
	return tokens;
}

laye_token lexer_read_laye_token(lexer_data *l)
{
	assert(not lexer_is_eof(*l), "lexer_read_laye_token called when at EoF");

	laye_token token;

	laye_trivia[] leadingTrivia = lexer_get_laye_trivia(l, false);
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
			diagnostics_add_error(l.diagnostics, source_span_create(startLocation, lexer_current_location(*l)), "invalid token `!` (did you mean `not`?)");
		}
	}
	else if (c == 34 /* " */)
		lexer_read_laye_string(l, &token);
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

			source_span ss = source_span_create(startLocation, lexer_current_location(*l));

			diagnostics_add_warning(l.diagnostics, ss, "invalid token `&&` (did you mean `and`?)");
			diagnostics_add_info(l.diagnostics, ss, "`&` can be either the bitwise and infix operator or the address-of prefix operator; `&&` could only legally appear as taking an address of an address inline, which is incredibly unlikely and must instead be written as `&(&variable)` if needed to avoid the much more common mistake of using `&&` as the logical and infix operator");
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

			diagnostics_add_warning(l.diagnostics, ss, "invalid token `||` (did you mean `or`?)");
			diagnostics_add_info(l.diagnostics, ss, "`|` is the bitwise or infix operator; `||` could never legally appear in an expression");
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

	if (token.kind == nil)
	{
		lexer_advance(l);

		string_builder errorBuilder;
		string_builder_append_string(&errorBuilder, "unrecognized character `");
		string_builder_append_rune(&errorBuilder, c);
		string_builder_append_string(&errorBuilder, "` (U+");
		string_builder_append_uint_hexn(&errorBuilder, cast(uint) c, 4);
		string_builder_append_string(&errorBuilder, ") in lexer");

		diagnostics_add_error(l.diagnostics, source_span_create(startLocation, lexer_current_location(*l)), string_builder_to_string(errorBuilder));

		string_builder_free(&errorBuilder);
	}

	token.sourceSpan = source_span_create(startLocation, lexer_current_location(*l));

	laye_trivia[] trailingTrivia = lexer_get_laye_trivia(l, true);
	token.trailingTrivia = trailingTrivia;

	return token;
}

void lexer_read_laye_identifier_or_number(lexer_data *l, laye_token *token)
{
	assert(rune_is_laye_identifier_part(lexer_current_rune(*l)), "lexer_read_laye_token called without identifier char");

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
		// the main lexer_read_laye_token function will calculate the sourceSpan for us, but we need it here to get the identifier value

		source_location endLocation = lexer_current_location(*l);
		token.sourceSpan = source_span_create(startLocation, endLocation);

		string ident = source_span_to_string(token.sourceSpan);
		token.kind = ::ident(ident);	
	}
}

void lexer_read_laye_radix_integer_from_delimiter(lexer_data *l, laye_token *token, source_location startLocation, u64 radix)
{
	panic("TODO(local): lexer_read_laye_radix_integer_from_delimiter is unimplemented");
}

i32 lexer_read_laye_escape_sequence_to_rune(lexer_data *l)
{
	source_location startLocation = lexer_current_location(*l);

	assert(lexer_current_rune(*l) == 92 /* \ */, "lexer_read_laye_escape_to_rune called without escape char");
	lexer_advance(l); // `\`

	if (lexer_current_rune(*l) == 85 /* U */ and lexer_peek_rune(*l) == 43 /* + */)
	{
		lexer_advance(l); // `U`
		lexer_advance(l); // `+`

		i32 codepointValue = 0;

		{
			uint i = 0;
			while (i < 4)
			{
				if (not rune_is_laye_digit_in_radix(lexer_current_rune(*l), 16))
				{
					diagnostics_add_error(l.diagnostics, source_span_create(startLocation, lexer_current_location(*l)), "expected 4 digits for unicode escape sequence");
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
	else
	{
		i32 result = lexer_current_rune(*l);

		lexer_advance(l); // next character, unrecognized
		diagnostics_add_error(l.diagnostics, source_span_create(startLocation, lexer_current_location(*l)), "unrecognized escape sequence");

		return result;
	}
}

void lexer_read_laye_string(lexer_data *l, laye_token *token)
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
		printf(">> %.*s%c", stringValue.length, stringValue.data, 10);
		string_builder_free(&sb);
	}
	else
	{
		string locationString = source_location_to_string(startLocation);
		printf("%.*s: error: unfinished string literal%c", locationString.length, locationString.data, 10);
	}
}
