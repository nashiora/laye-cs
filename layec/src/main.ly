/*

// X finished
// ! in progress
// T needs testing, but looks to be implemented

[ ] Full type implementations
    [ ] Rune type (and basic string UTF8 reading associated with it)
[ ] Operator expressions
    [T] Infix ops (+, -, *, /, %, <<, >>, &, |, ~, <, >, ==, !=, <=, >=, and, or)
    [T] Prefix ops (&, *, -, not)
    [ ] Explicit type casts
[ ] Structure operations
    [X] Size of structure `sizeof(Type)`
    [ ] Offset of structure element (optional?) `offsetof(Type, field_name)`
    [ ] Structural initializers
    [X] Zero initializers
[ ] Branch structures
    [X] If/else `if (expr) { } else if (expr) { } else { }
    [X] While `while (expr) { } else { }`
    [ ] C-style For `for (binding or expr; expr; expr) { } else { }`
    [ ] Switch statements `switch (expr) { case Constant: <scoped exprs, no break needed> default: <same> }`
[ ] Tagged unions
    [ ] Enum syntax `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for enums `switch (expr) { case EnumName::VariantName: <scoped exprs, no break needed> case ::VariantName <same> default: <same> }`
    [ ] Union syntax (optional) `enum EnumName { VariantName }` `enum EnumName { VariantName(int VariantField) }`
    [ ] Switch statement support for unions (optional) `switch (expr) { case UnionName::VariantName: <scoped exprs, no break needed> default: <same> }`

*/

// TODO(local): compile `main` into `_laye_start`, overwrite the entry point with actual startup logic to turn this into a string slice
void main(i32 argc, u8 readonly[*] readonly[*] argv)
{
    // program startup logic (if we keep the idea of passing the program args to main like C and friends)
    string[*] argvStorage = memory_allocate(sizeof(string) * argc);
    uint argvCounter = 0;
    while (argvCounter < argc)
    {
        argvStorage[argvCounter] = argv[argvCounter][:strlen(argv[argvCounter])];
        argvCounter = argvCounter + 1;
    }
    string[] args = argvStorage[:argc];
    // end program startup logic

    laye_main(args);
}

void laye_main(string[] args)
{
    printf("Laye stand-alone compiler%cVersion 0.1.0%c", 10, 10);

    {
        printf("invoked with the following arguments:%c", 10);

        uint i = 0;
        while (i < args.length)
        {
            printf("  %.*s%c", args[i].length, args[i].data, 10);
            i = i + 1;
        }
    }

    source sourceFile = source_create_from_file("./layec/src/main.ly");
    if (not sourceFile.isValid)
    {
        printf("failed to open file%c", 10);
        return;
    }
    
    printf("opened file successfully%c", 10);

    //printf("%c%.*s%c", 10, sourceFile.text.length, sourceFile.text.data, 10);

    laye_token_list sourceTokenList = lexer_read_laye_tokens(sourceFile);
    if (not sourceTokenList.isValid)
    {
        printf("failed to read tokens from file%c", 10);
        //return;
    }

    laye_token[] sourceTokens = laye_token_list_get_tokens(sourceTokenList);
    
    printf("read %llu tokens from file successfully%c", sourceTokens.length, 10);

    {
        uint i = 0;
        while (i < sourceTokens.length)
        {
            laye_token token = sourceTokens[i];

            string tokenLocationString = source_location_to_string(token.sourceSpan.startLocation);
            string tokenString = source_span_to_string(token.sourceSpan);

            printf("> token (");
            printf("%.*s", tokenLocationString.length, tokenLocationString.data);
            printf(")%c", 10);

            printf("  `%.*s`%c", tokenString.length, tokenString.data, 10);

            {
                printf("    leading trivia%c", 10);
                uint j = 0;
                while (j < token.leadingTrivia.length)
                {
                    laye_trivia trivia = token.leadingTrivia[j];
                    string triviaString = source_span_to_string(trivia.sourceSpan);
                    printf("    `%.*s`%c", triviaString.length, triviaString.data, 10);
                    j = j + 1;
                }
            }

            {
                printf("    trailing trivia%c", 10);
                uint j = 0;
                while (j < token.trailingTrivia.length)
                {
                    laye_trivia trivia = token.trailingTrivia[j];
                    string triviaString = source_span_to_string(trivia.sourceSpan);
                    printf("    `%.*s`%c", triviaString.length, triviaString.data, 10);
                    j = j + 1;
                }
            }

            i = i + 1;
        }
    }
}
