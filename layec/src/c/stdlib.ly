
extern "C" rawptr calloc(uint nitems, uint size);
extern "C" void free(rawptr ptr);
extern "C" rawptr malloc(uint size);
extern "C" rawptr realloc(rawptr ptr, uint size);

extern "C" void exit(i32 status);
extern "C" void abort();
