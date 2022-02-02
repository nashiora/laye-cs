
// HANDLE WINAPI GetStdHandle(DWORD nStdHandle);
extern "kernel32" stdcall i32 GetStdHandle(i32 nStdHandle);

// BOOL WINAPI WriteConsole(HANDLE hConsoleOutput, const VOID *lpBuffer, DWORD nNumberOfCharsToWrite, LPDWORD lpNumberOfCharsWritten, LPVOID lpReserved);
//extern "kernel32" stdcall i32 WriteConsoleA(i32 hConsoleOutput, u8 [*]lpBuffer, i32 nNumberOfCharsToWrite, i32 *lpNumberOfCharsWritten, rawptr lpReserved);
extern "kernel32" stdcall i32 WriteConsoleA(i32 hConsoleOutput, u8 [*]lpBuffer, i32 nNumberOfCharsToWrite, rawptr lpNumberOfCharsWritten, rawptr lpReserved);
