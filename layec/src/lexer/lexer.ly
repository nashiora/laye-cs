
struct laye_lexer
{
	// TODO(local): true/false literals
	bool is_valid;
}

struct c_lexer
{
	bool is_valid;
}

/*
FILE *f = fopen("textfile.txt", "rb");
fseek(f, 0, SEEK_END);
long fsize = ftell(f);
fseek(f, 0, SEEK_SET);

char *string = malloc(fsize + 1);
fread(string, fsize, 1, f);
fclose(f);

string[fsize] = 0;
*/
