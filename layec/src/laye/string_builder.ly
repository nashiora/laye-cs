struct string_builder
{
    // private
    // u8 [dynamic]value;
    uint length;
    uint capacity;
    u8[*] data;
}

void string_builder_init(string_builder *sb)
{
	rawptr data = 0;
	(*sb).length = 0;
	(*sb).capacity = 0;
	(*sb).data = data;
}

uint string_builder_length_get(string_builder sb)
{
	return sb.length;
}

uint string_builder_capacity_get(string_builder sb)
{
	return sb.capacity;
}

u8[*] string_builder_data_get(string_builder sb)
{
	return sb.data;
}

void string_builder_ensure_capacity(string_builder *sb, uint desired_capacity)
{
	uint capacity = (*sb).capacity;
	if (capacity < desired_capacity)
	{
		(*sb).capacity = desired_capacity;
		(*sb).data = realloc((*sb).data, desired_capacity);
	}
}

void string_builder_append_string(string_builder *sb, string v)
{
	uint sb_length = string_builder_length_get(*sb);
	uint required_capacity = sb_length + v.length;
	string_builder_ensure_capacity(sb, required_capacity);

	uint i = 0;
	while (i < v.length)
	{
		(*sb).data[sb_length + i] = v.data[i];
		i = i + 1;
	}

	(*sb).length = required_capacity;
}

string string_builder_to_string(string_builder sb)
{
	u8[*] data = string_builder_data_get(sb);
	uint length = string_builder_length_get(sb);

	u8[*] result_data = malloc(sizeof(u8) * length);
	memcpy(result_data, data, length);

	return result_data[:length];
}
