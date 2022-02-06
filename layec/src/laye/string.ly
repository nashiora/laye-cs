//struct __laye_string { u8 readonly [*]value; }

string string_concat(string a, string b)
{
    string_builder builder;
    //string_builder_append_string(&builder, a);
    //string_builder_append_string(&builder, b);
    return string_builder_to_string(builder);
}
