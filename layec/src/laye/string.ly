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

    string result = string_builder_to_string(builder);
    string_builder_free(&builder);
    return result;
}

string uint_to_string(uint v)
{
    string_builder builder;
    string_builder_append_uint(&builder, v);
    
    string result = string_builder_to_string(builder);
    string_builder_free(&builder);
    return result;
}

bool char_is_digit(u8 c)
{
    /* 0-9 */
    return c >= 48 and c < 58;
}

bool char_is_alpha(u8 c)
{
    /* A-Z a-z */
    return (c >= 65 and c <= 90) or (c >= 97 and c <= 122);
}

bool char_is_white_space(u8 c)
{
    // \t  \n  \v  \r  ' '
    return c == 9 or c == 10 or c == 11 or c == 13 or c == 32;
}
