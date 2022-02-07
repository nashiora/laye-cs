
// HANDLE WINAPI GetStdHandle(DWORD nStdHandle);
extern "kernel32" i32 stdcall GetStdHandle(i32 nStdHandle);

// BOOL WINAPI WriteConsole(HANDLE hConsoleOutput, const VOID *lpBuffer, DWORD nNumberOfCharsToWrite, LPDWORD lpNumberOfCharsWritten, LPVOID lpReserved);
//extern "kernel32" i32 stdcall WriteConsoleA(i32 hConsoleOutput, u8 [*]lpBuffer, i32 nNumberOfCharsToWrite, i32 *lpNumberOfCharsWritten, rawptr lpReserved);
extern "kernel32" i32 stdcall WriteConsoleA(i32 hConsoleOutput, u8 readonly[*]lpBuffer, i32 nNumberOfCharsToWrite, rawptr lpNumberOfCharsWritten, rawptr lpReserved);

//http://alter.org.ua/en/docs/win/args/
extern "kernel32" u8[*] stdcall GetCommandLineA();
string GetCommandLine()
{
    u8[*] cmdarg = GetCommandLineA();
    return cmdarg[:strlen(cmdarg)];
}
