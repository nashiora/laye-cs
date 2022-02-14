
struct laye_lexer
{
	// TODO(local): true/false literals
	bool is_valid;
}

struct c_lexer
{
	bool is_valid;
}

bool laye_lexer_open_file(laye_lexer *ll, string fileName)
{
	rawptr fileHandle = fopen(string_to_cstring(fileName), "r");
	if (fileHandle == nullptr)
		return false;

	fclose(fileHandle);
	return true;
}
