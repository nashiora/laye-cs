// note that this does not fully implement the readonly nature of some types, which is not really necessary given we aren't providing our own implementations
// we also convert most pointer types to rawptr since opaque types don't exist yet
// 
// list of functions from here: https://www.tutorialspoint.com/c_standard_library/stdio_h.htm
// because why not
//
extern "C" i32 cdecl fclose(rawptr stream);
extern "C" void cdecl clearerr(rawptr stream);
extern "C" i32 cdecl feof(rawptr stream);
extern "C" i32 cdecl ferror(rawptr stream);
extern "C" i32 cdecl fflush(rawptr stream);
extern "C" i32 cdecl fgetpos(rawptr stream, rawptr pos);
extern "C" rawptr cdecl fopen(u8 readonly[*] filename, u8 readonly[*] mode);
extern "C" uint cdecl fread(rawptr ptr, uint size, uint nmemb, rawptr stream);
extern "C" rawptr cdecl freopen(u8 readonly[*] filename, u8 readonly[*] mode, rawptr stream);
extern "C" i32 cdecl fseek(rawptr stream, i64 offset, i32 whence);
extern "C" i32 cdecl fsetpos(rawptr stream, rawptr pos);
extern "C" i64 cdecl ftell(rawptr stream);
extern "C" uint cdecl fwrite(rawptr ptr, uint size, uint nmemb, rawptr stream);
extern "C" i32 cdecl remove(u8 readonly[*] filename);
extern "C" i32 cdecl rename(u8 readonly[*] old_filename, u8 readonly[*] new_filename);
extern "C" void cdecl rewind(rawptr stream);
extern "C" void cdecl setbuf(rawptr stream, u8[*] buffer);
extern "C" i32 cdecl setvbuf(rawptr stream, u8[*] buffer, i32 mode, u32 size);
extern "C" rawptr cdecl tmpfile();
extern "C" u8[*] cdecl tmpnam(u8[*] str);
extern "C" i32 cdecl fprintf(rawptr stream, u8 readonly[*] format, varargs);
extern "C" i32 cdecl printf(u8 readonly[*] format, varargs);
extern "C" i32 cdecl sprintf(u8 [*]str, u8 readonly[*] format, varargs);
//extern "C" i32 cdecl vfprintf(rawptr stream, u8 readonly[*] format, va_list arg);
//extern "C" i32 cdecl vprintf(u8 readonly[*] format, va_list arg);
//extern "C" i32 cdecl vsprintf(u8[*] str, u8 readonly[*] format, va_list arg);
extern "C" i32 cdecl fscanf(rawptr stream, u8 readonly[*] format, varargs);
extern "C" i32 cdecl scanf(u8 readonly[*] format, varargs);
extern "C" i32 cdecl sscanf(u8 readonly[*] str, u8 readonly[*] format, varargs);
extern "C" i32 cdecl fgetc(rawptr stream);
extern "C" u8[*] cdecl fgets(u8[*] str, i32 n, rawptr stream);
extern "C" i32 cdecl fputc(i32 char, rawptr stream);
extern "C" i32 cdecl fputs(u8 readonly[*] str, rawptr stream);
extern "C" i32 cdecl getc(rawptr stream);
extern "C" i32 cdecl getchar();
extern "C" u8[*] cdecl gets(u8[*] str);
extern "C" i32 cdecl putc(i32 char, rawptr stream);
extern "C" i32 cdecl putchar(i32 char);
extern "C" i32 cdecl puts(u8 readonly[*] str);
extern "C" i32 cdecl ungetc(i32 char, rawptr stream);
extern "C" void cdecl perror(u8 readonly[*] str);