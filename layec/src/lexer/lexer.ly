
struct laye_lexer
{
	// TODO(local): true/false literals
	bool is_valid;
}

struct c_lexer
{
	bool is_valid;
}

void laye_lexer_open_file(laye_lexer *ll, string fileName)
{
	rawptr fileHandle = fopen(string_to_cstring(fileName), "r");
	// TODO(local): need nullptr to check for C errors now
	fclose(fileHandle);
}
