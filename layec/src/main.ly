
struct test_struct
{
    int a;
    readonly int b;
}

struct __laye_context
{
    rawptr nocontext(uint, rawptr) allocatorFunction;
}

/*
rawptr nocontext default_allocator(uint size, rawptr memory)
{
    if (size > 0)
    {
        if (memory != nullptr)
            return realloc(memory, size);

        return malloc(size);
    }

    free(memory);
    return nullptr;
}
*/

void test()
{
    printf("test (with context)%c", 10);
}

void nocontext test_nocontext()
{
    printf("test (without context)%c", 10);
}

void main()
{
    __laye_context lcontext;

    string testString = "Hello, hunter!";
    uint stringLength = testString.length;
    printf("stringLength = %llu%c", stringLength, 10);

    u8[*] cmdarg = GetCommandLineA();
    printf("%s%c", cmdarg, 10);

    uint cmdargLength = strlen(cmdarg);
    u8[] cmdargSlice = cmdarg[:cmdargLength];

    string cmdargString = cmdargSlice;

    test();
    test_nocontext();

    //i32 consoleHandle = GetStdHandle(-11);
    //WriteConsoleA(consoleHandle, "Hello, hunter! from Win32 offscreen", 25, 0, 0);
    //printf("%c", 10);
}
