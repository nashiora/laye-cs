struct string_builder
{
    // private
    // u8 [dynamic]value;
}

void string_builder_append_string(string_builder *sb, string v)
{
	// TODO(local): how to really handle these kinds of containers + how to handle strings and their fields
	// dynamic_ensure_capacity(sb.value.length + v.length);
}

string string_builder_to_string(string_builder sb)
{
	return "";
}
