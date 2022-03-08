bool parser_read_all_laye_nodes(parser_data *p, syntax_node[dynamic] *nodes)
{
	if (not p.lexer.source.isValid)
		return false;

	p.parseContext = ::laye;
	parser_advance(p); // prime the parser with the first token

	while (not parser_is_eof(p))
	{
		parser_advance(p);
	}

	return true;
}

syntax_node parser_read_laye_top_level_declaration(parser_data *p)
{
	syntax_node result;
	return result;
}

syntax_node parser_read_laye_using(parser_data *p)
{
	syntax_token tkUsing = parser_current_token(p);

	// TODO(local): is expression
	// TODO(local): assert
	if (tkUsing.kind is not ::kw_using)
		panic("parser_read_laye_using called without `using` keyword");

	syntax_node result;
	return result;
}

syntax_node parser_read_laye_namespace(parser_data *p)
{
	syntax_token tkNamespace = parser_current_token(p);

	// TODO(local): is expression
	// TODO(local): assert
	if (tkNamespace.kind is not ::kw_namespace)
		panic("parser_read_laye_using called without `namespace` keyword");

	syntax_node result;
	return result;
}

/* A Laye annotation begins with `#[` and ends with `]` and contains either compile time statements or custom data.
 * A common compile time statement is the `if` annotation used for conditional compilation.
 * The `if` annotation requires an expression context to be enabled in the parser so some identifiers conditionally become keywords.
 */
syntax_node parser_read_laye_annotation(parser_data *p)
{
	syntax_token tkOpenAnnotation = parser_current_token(p);

	// TODO(local): is expression
	// TODO(local): assert
	if (tkOpenAnnotation.kind is not ::hash_open_bracket)
		panic("parser_read_laye_using called without `#[` delimiter");

	syntax_node result;
	return result;
}
