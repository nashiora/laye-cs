struct string_builder
{
	// private
	// u8 [dynamic]value;
	uint length;
	uint capacity;
	u8[*] data;
}

uint string_builder_length_get(string_builder sb) { return sb.length; }
uint string_builder_capacity_get(string_builder sb) { return sb.capacity; }
u8 readonly[*] string_builder_data_get(string_builder sb) { return sb.data; }

void string_builder_ensure_capacity(string_builder *sb, uint desired_capacity)
{
	uint capacity = (*sb).capacity;
	if (capacity < desired_capacity)
	{
		(*sb).capacity = desired_capacity;
		(*sb).data = realloc((*sb).data, desired_capacity);
	}
}

string string_builder_to_string(string_builder sb)
{
	u8 readonly[*] data = string_builder_data_get(sb);
	uint length = string_builder_length_get(sb);

	u8[*] result_data = malloc(sizeof(u8) * length);
	memcpy(result_data, data, length);

	return result_data[:length];
}

void string_builder_append_string(string_builder *sb, string v)
{
	uint sbLength = string_builder_length_get(*sb);
	uint requiredCapacity = sbLength + v.length;
	string_builder_ensure_capacity(sb, requiredCapacity);

	uint i = 0;
	while (i < v.length)
	{
		(*sb).data[sbLength + i] = v.data[i];
		i = i + 1;
	}

	(*sb).length = requiredCapacity;
}

void string_builder_append_character(string_builder *sb, u8 c)
{
	uint sbLength = string_builder_length_get(*sb);
	uint requiredCapacity = sbLength + 1;
	string_builder_ensure_capacity(sb, requiredCapacity);

	(*sb).data[sbLength] = c;
	(*sb).length = requiredCapacity;
}

void string_builder_append_uint(string_builder *sb, uint v)
{
	uint sbLength = string_builder_length_get(*sb);
	uint digitCount = uint_num_digits(v);
	uint requiredCapacity = sbLength + digitCount;
	string_builder_ensure_capacity(sb, requiredCapacity);

	uint i = 0;
	while (v > 0)
	{
		uint d = v % 10;
		uint index = sbLength + digitCount - i - 1;
		u8 c = cast(u8)(48 + d);
		(*sb).data[index] = c;

		v = v / 10;
		i = i + 1;
	}

	(*sb).length = requiredCapacity;
}

/*
void string_builder_append_int(string_builder *sb, int v)
{
	if (v < 0)
	{
		string_builder_append_string("-");
		v = -v;
	}

	//string_builder_append_uint(cast(uint) v);
}
*/
