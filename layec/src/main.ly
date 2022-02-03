
void test()
{
    printf("test (with context)%c", 10);
}

nocontext void test_nocontext()
{
    printf("test (without context)%c", 10);
}

void main()
{
    u8 [*]cmdarg = GetCommandLineA();
    printf("%s%c", cmdarg, 10);

    test();
    test_nocontext();

    //i32 consoleHandle = GetStdHandle(-11);
    //WriteConsoleA(consoleHandle, "Hello, hunter! from Win32 offscreen", 25, 0, 0);
    //printf("%c", 10);
}
