void main()
{
    printf("Hello, hunter! from C stdlib%c", 10);

    i32 consoleHandle = GetStdHandle(-11);
    WriteConsoleA(consoleHandle, "Hello, hunter! from Win32", 25, 0, 0);

    printf("%c", 10);
}
