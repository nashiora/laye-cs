struct parser_data
{
	lexer_data lexer;

	syntax_token currentToken;
	parser_context_kind parseContext;

	diagnostic_bag *diagnostics;
}

enum parser_context_kind
{
	laye,
}

void parser_init(parser_data *p, source source, diagnostic_bag *diagnostics)
{
	lexer_data l;
	l.diagnostics = diagnostics;
	l.source = source;
	l.currentLine = 1;
	l.currentColumn = 1;

	p.lexer = l;
	p.diagnostics = diagnostics;
}

syntax_token parser_current_token(parser_data *p)
{
	return p.currentToken;
}

bool parser_is_eof(parser_data *p)
{
	if (parser_current_token(p).kind is ::eof)
		return true;

	return lexer_is_eof(p.lexer);
}

void parser_advance(parser_data *p)
{
	uint currentIndex = p.lexer.currentIndex;

	if (p.parseContext == parser_context_kind::laye)
		p.currentToken = lexer_read_laye_token(&p.lexer);
	else panic("unknown parse context when advancing");

	// if we didn't advance, panic
	assert(currentIndex != p.lexer.currentIndex, "internal Laye lexer error: call to `lexer_read_syntax_token` did not consume any characters");
	assert(parser_current_token(p).kind != nil, "internal Laye lexer error: call to `lexer_read_syntax_token` returned a nil-kinded token");
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
