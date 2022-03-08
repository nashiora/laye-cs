#[if target.os == ::windows]
namespace laye::console;

void write_string(string message)
{
	rawptr stdoutHandle = global::win32::get_std_handle(global::win32::STD_OUTPUT_HANDLE);
	if (stdoutHandle == nullptr or stdoutHandle == global::win32::INVALID_HANDLE_VALUE)
		return;

	i32 nCharsWritten;
	i32 writeResult = global::win32::write_console(stdoutHandle, message.data, cast(i32) message.length, &nCharsWritten, nullptr);
}
