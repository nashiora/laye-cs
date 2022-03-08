struct lexer_data
{
	source source;
	uint currentIndex;

	uint currentColumn;
	uint currentLine;

	diagnostic_bag *diagnostics;

	/* Enables things like the `target` keyword in annotations. */
	bool contextLayeCompileTimeExpression;
}

bool lexer_is_eof(lexer_data l)
{
	return l.currentIndex >= l.source.text.length or l.source.text[l.currentIndex] == 0;
}

i32 lexer_current_rune(lexer_data l)
{
	if (lexer_is_eof(l)) return 0;
	//return cast(i32) l.source.text[l.currentIndex];
	return unicode_utf8_string_rune_at_index(l.source.text, l.currentIndex);
}

i32 lexer_peek_rune(lexer_data l)
{
	if (lexer_is_eof(l)) return 0;
	uint currentByteCount = unicode_utf8_calc_encoded_byte_count(l.source.text[l.currentIndex]);

	//if (l.currentIndex + currentByteCount >= l.source.text.length) return 0;
	//return cast(i32) l.source.text[l.currentIndex + 1];
	return unicode_utf8_string_rune_at_index(l.source.text, l.currentIndex + currentByteCount); // this function checks for bounds errors etc. and returns 0 if so
}

source_location lexer_current_location(lexer_data l)
{
	source_location location;
	location.source = l.source;
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
