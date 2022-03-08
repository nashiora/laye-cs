#[if target.os == ::windows]
namespace win32;

const rawptr INVALID_HANDLE_VALUE = cast(rawptr) -1;

const i32 STD_OUTPUT_HANDLE = -11;

foreign "WriteConsoleA" callconv(stdcall)
i32 write_console( rawptr hConsoleOutput
				 , u8 readonly[*] lpBuffer
				 , i32 nNumberOfCharsToWrite
				 , i32 writeonly* lpNumberOfCharsWritten
				 , rawptr lpReserved);

foreign "GetStdHandle" callconv(stdcall)
rawptr get_std_handle(i32 nStdHandle);
