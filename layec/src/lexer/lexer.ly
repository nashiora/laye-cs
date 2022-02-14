
struct laye_lexer
{
	uint currentIndex;

	uint currentColumn;
	uint currentLine;
}

struct c_lexer
{
}

struct laye_token
{
	source_span sourceSpan;
	bool is_valid;
}

struct laye_token_list
{
	laye_token[] tokens;
	bool is_valid;
}

laye_token_list laye_lexer_read_tokens(source source)
{
	laye_token_list resultTokens;

	if (not source.is_valid)
		return resultTokens;

	string sourceText = source.text;

	resultTokens.is_valid = true;

	return resultTokens;
}
