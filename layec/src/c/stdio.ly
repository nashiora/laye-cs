// note that this does not fully implement the readonly nature of some types, which is not really necessary given we aren't providing our own implementations
// we also convert most pointer types to rawptr since opaque types don't exist yet
// 
// list of functions from here: https://www.tutorialspoint.com/c_standard_library/stdio_h.htm
// because why not
extern "C" i32 fclose(rawptr stream);
extern "C" void clearerr(rawptr stream);
extern "C" i32 feof(rawptr stream);
extern "C" i32 ferror(rawptr stream);
extern "C" i32 fflush(rawptr stream);
extern "C" i32 fgetpos(rawptr stream, rawptr pos);
extern "C" rawptr fopen(u8 readonly[*]filename, u8 readonly[*]mode);
extern "C" u32 fread(rawptr ptr, u32 size, u32 nmemb, rawptr stream);
extern "C" rawptr freopen(u8 readonly[*]filename, u8 readonly[*]mode, rawptr stream);
extern "C" i32 fseek(rawptr stream, i64 offset, i32 whence);
extern "C" i32 fsetpos(rawptr stream, rawptr pos);
extern "C" i64 ftell(rawptr stream);
extern "C" u32 fwrite(rawptr ptr, u32 size, u32 nmemb, rawptr stream);
extern "C" i32 remove(u8 readonly[*]filename);
extern "C" i32 rename(u8 readonly[*]old_filename, u8 readonly[*]new_filename);
extern "C" void rewind(rawptr stream);
extern "C" void setbuf(rawptr stream, u8 [*]buffer);
extern "C" i32 setvbuf(rawptr stream, u8 [*]buffer, i32 mode, u32 size);
extern "C" rawptr tmpfile();
extern "C" u8 [*]tmpnam(u8 [*]str);
extern "C" i32 fprintf(rawptr stream, u8 readonly[*]format, varargs);
extern "C" i32 printf(u8 readonly[*]format, varargs);
extern "C" i32 sprintf(u8 [*]str, u8 readonly[*]format, varargs);
//extern "C" i32 vfprintf(rawptr stream, u8 readonly[*]format, va_list arg);
//extern "C" i32 vprintf(u8 readonly[*]format, va_list arg);
//extern "C" i32 vsprintf(u8 [*]str, u8 readonly[*]format, va_list arg);
extern "C" i32 fscanf(rawptr stream, u8 readonly[*]format, varargs);
extern "C" i32 scanf(u8 readonly[*]format, varargs);
extern "C" i32 sscanf(u8 readonly[*]str, u8 readonly[*]format, varargs);
extern "C" i32 fgetc(rawptr stream);
extern "C" u8 [*]fgets(u8 [*]str, i32 n, rawptr stream);
extern "C" i32 fputc(i32 char, rawptr stream);
extern "C" i32 fputs(u8 readonly[*]str, rawptr stream);
extern "C" i32 getc(rawptr stream);
extern "C" i32 getchar();
extern "C" u8 [*]gets(u8 [*]str);
extern "C" i32 putc(i32 char, rawptr stream);
extern "C" i32 putchar(i32 char);
extern "C" i32 puts(u8 readonly[*]str);
extern "C" i32 ungetc(i32 char, rawptr stream);
extern "C" void perror(u8 readonly[*]str);