
string system_command_line_get()
{
    // get the command line arguments from Windows for now
    u8[*] cmdarg = GetCommandLineA();
    return cmdarg[:strlen(cmdarg)];
}