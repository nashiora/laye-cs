
bool char_is_laye_identifier_part(i32 c)
{
	return char_is_digit(c) or char_is_alpha(c) or c == 95 /* `_` */;
}

struct lexer_data
{
	source currentSource;
	uint currentIndex;

	uint currentColumn;
	uint currentLine;
}

struct laye_trivia
{
	source_span sourceSpan;
	bool isValid;
}

struct laye_trivia_list
{
	laye_trivia[*] buffer;
	uint capacity;
	uint length;

	bool isValid;
}

struct laye_token
{
	source_span sourceSpan;

	laye_trivia[] leadingTrivia;
	laye_trivia[] trailingTrivia;

	bool isValid;
}

struct laye_token_list
{
	laye_token[*] buffer;
	uint capacity;
	uint length;

	bool isValid;
}

bool lexer_is_eof(lexer_data l)
{
	return l.currentIndex >= l.currentSource.text.length;
}

i32 lexer_current_char(lexer_data l)
{
	if (lexer_is_eof(l)) return 0;
	return cast(i32) l.currentSource.text[l.currentIndex];
}

i32 lexer_peek_char(lexer_data l)
{
	if (l.currentIndex + 1 >= l.currentSource.text.length) return 0;
	return cast(i32) l.currentSource.text[l.currentIndex + 1];
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

	i32 c = lexer_current_char(*l);
	(*l).currentIndex = (*l).currentIndex + 1;

	if (c == 10)
	{
		(*l).currentLine = (*l).currentLine + 1;
		(*l).currentColumn = 1;
	}
	else (*l).currentColumn = (*l).currentColumn + 1;
}

laye_trivia[] lexer_get_laye_trivia(lexer_data *l, bool untilEndOfLine)
{
	laye_trivia_list triviaList;

	while (not lexer_is_eof(*l))
	{
		laye_trivia trivia;
		source_location startLocation = lexer_current_location(*l);

		i32 c = lexer_current_char(*l);
		//printf("here (before checking trivia kind) (%d, %d)%c", c, lexer_peek_char(*l), 10);

		if (char_is_white_space(c))
		{
			//printf("here (in white space start)%c", 10);
			while (not lexer_is_eof(*l) and char_is_white_space(lexer_current_char(*l)))
			{
				c = lexer_current_char(*l);
				lexer_advance(l);

				if (c == 10 and untilEndOfLine)
					break;
			}

			source_location endLocation = lexer_current_location(*l);

			trivia.isValid = true;
			trivia.sourceSpan = source_span_create(startLocation, endLocation);
		}
		else if (c == 47 /* `/` */ and lexer_peek_char(*l) == 47 /* `/` */)
		{
			//printf("here (in line comment start)%c", 10);
			while (not lexer_is_eof(*l))
			{
				c = lexer_current_char(*l);
				lexer_advance(l);

				if (c == 10) break;
			}

			source_location endLocation = lexer_current_location(*l);

			trivia.isValid = true;
			trivia.sourceSpan = source_span_create(startLocation, endLocation);
		}
		else if (c == 47 /* `/` */ and lexer_peek_char(*l) == 42 /* `*` */)
		{
			//printf("here (in block comment start)%c", 10);
			lexer_advance(l); // `/`
			lexer_advance(l); // `*`

			uint nesting = 1;

			while (not lexer_is_eof(*l))
			{
				c = lexer_current_char(*l);
				lexer_advance(l);

				if (c == 47 /* `/` */ and lexer_current_char(*l) == 42 /* `*` */)
				{
					lexer_advance(l); // `*`
					nesting = nesting + 1;
				}
				else if (c == 42 /* `*` */ and lexer_current_char(*l) == 47 /* `/` */)
				{
					lexer_advance(l); // `/`
					nesting = nesting - 1;

					// TODO(local): implement break/continue
					if (nesting == 0) break;
				}
			}

			source_location endLocation = lexer_current_location(*l);
			trivia.sourceSpan = source_span_create(startLocation, endLocation);

			if (nesting > 0)
			{
				string locationString = source_location_to_string(endLocation);
				printf("%.*s: error: unfinished block comment%c", locationString.length, locationString.data, 10);

				trivia.isValid = false;
				break;
			}
			else trivia.isValid = true;
		}
		else break;

		if (trivia.isValid)
			laye_trivia_list_append_trivia(&triviaList, trivia);
		// TODO(local): implement break/continue
		else break;
	}

	return laye_trivia_list_get_trivia(triviaList);
}

laye_token_list lexer_read_laye_tokens(source source)
{
	laye_token_list resultTokens;

	if (not source.isValid)
		return resultTokens;

	lexer_data l;
	l.currentSource = source;
	l.currentLine = 1;
	l.currentColumn = 1;

	while (not lexer_is_eof(l))
	{
		printf("  reading token...%c", 10);

		uint currentIndex = l.currentIndex;
		laye_token token = lexer_read_laye_token(&l);

		// if we didn't advance, panic
		if (currentIndex == l.currentIndex)
		{
			printf("  internal Laye lexer error: call to `lexer_read_laye_token` did not consume any characters%c", 10);
			return resultTokens;
		}

		if (not token.isValid)
			return resultTokens;

		laye_token_list_append_token(&resultTokens, token);
	}

	resultTokens.isValid = true;
	return resultTokens;
}

laye_token lexer_read_laye_token(lexer_data *l)
{
	assert(not lexer_is_eof(*l), "lexer_read_laye_token called when at EoF");

	laye_token token;

	laye_trivia[] leadingTrivia = lexer_get_laye_trivia(l, false);
	token.leadingTrivia = leadingTrivia;

	i32 c = lexer_current_char(*l);

	if (char_is_laye_identifier_part(c))
	{
		token = lexer_read_laye_identifier(l);
	}
	else
	{
		source_location location = lexer_current_location(*l);

		string locationString = source_location_to_string(location);
		printf("%.*s: error: unrecognized character `%c` in lexer%c", locationString.length, locationString.data, c, 10);

		lexer_advance(l);

		token.sourceSpan = source_span_create(location, lexer_current_location(*l));
		token.isValid = false;
	}

	laye_trivia[] trailingTrivia = lexer_get_laye_trivia(l, true);
	token.trailingTrivia = trailingTrivia;

	return token;
}

laye_token lexer_read_laye_identifier(lexer_data *l)
{
	assert(char_is_laye_identifier_part(lexer_current_char(*l)), "lexer_read_laye_token called when at EoF");

	laye_token token;
	source_location startLocation = lexer_current_location(*l);

	while (not lexer_is_eof(*l) and char_is_laye_identifier_part(lexer_current_char(*l)))
		lexer_advance(l);

	source_location endLocation = lexer_current_location(*l);

	token.isValid = true;
	token.sourceSpan = source_span_create(startLocation, endLocation);

	return token;
}

laye_trivia[] laye_trivia_list_get_trivia(laye_trivia_list trivia)
{
	return trivia.buffer[:trivia.length];
}

void laye_trivia_list_append_trivia(laye_trivia_list *trivia, laye_trivia trivium)
{
	// TODO(local): dynamic arrays
	if ((*trivia).length == (*trivia).capacity)
	{
		uint capacity;
		if ((*trivia).capacity == 0)
			capacity = 32;
		else capacity = (*trivia).capacity * 2;

		(*trivia).buffer = realloc((*trivia).buffer, capacity * sizeof(laye_trivia));
		(*trivia).capacity = capacity;
	}

	(*trivia).buffer[(*trivia).length] = trivium;
	(*trivia).length = (*trivia).length + 1;
}


laye_token[] laye_token_list_get_tokens(laye_token_list tokens)
{
	return tokens.buffer[:tokens.length];
}

void laye_token_list_append_token(laye_token_list *tokens, laye_token token)
{
	// TODO(local): dynamic arrays
	if ((*tokens).length == (*tokens).capacity)
	{
		if ((*tokens).capacity == 0)
		{
			(*tokens).capacity = 32;
			(*tokens).buffer = malloc(32 * sizeof(laye_token));
		}
		else
		{
			(*tokens).capacity = (*tokens).capacity * 2;
			(*tokens).buffer = realloc((*tokens).buffer, (*tokens).capacity * sizeof(laye_token));
		}
	}

	(*tokens).buffer[(*tokens).length] = token;
	(*tokens).length = (*tokens).length + 1;
}
