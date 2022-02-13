u8[*] string_to_cstring(string s)
{
    u8 readonly[*] data = s.data;

    u8[*] cstring = malloc(s.length + 1);
    // u8[*] cstring = context.alloc(s.length + 1);
    // u8[*] cstring = allocate(s.length + 1); // allocate has a context!
    memcpy(cstring, data, s.length);
    cstring[s.length] = 0;

    return cstring;
}

string string_concat(string a, string b)
{
    string_builder builder;
    string_builder_ensure_capacity(&builder, a.length + b.length);
    string_builder_append_string(&builder, a);
    string_builder_append_string(&builder, b);
    return string_builder_to_string(builder);
}
