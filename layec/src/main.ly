
struct test_struct
{
    int a;
    readonly int b;
}

struct __laye_context
{
    rawptr nocontext(uint, rawptr) allocatorFunction;
}

void test()
{
    printf("test (with context)%c", 10);
}

void nocontext test_nocontext()
{
    printf("test (without context)%c", 10);
    return;
    printf("this will never run%c", 10);
}

void main()
{
    __laye_context lcontext;

    u8 [*]cmdarg = GetCommandLineA();
    printf("%s%c", cmdarg, 10);

    test();
    test_nocontext();

    //i32 consoleHandle = GetStdHandle(-11);
    //WriteConsoleA(consoleHandle, "Hello, hunter! from Win32 offscreen", 25, 0, 0);
    //printf("%c", 10);
}
