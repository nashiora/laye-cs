
struct source
{
	string name;
	string text;

	bool is_valid;
}

struct source_location
{
	source source;

	uint characterIndex;

	uint lineNumber;
	uint columnNumber;
}

struct source_span
{
	source_location startLocation;
	source_location endLocation;
}

source source_create_from_file(string fileName)
{
	i32 SEEK_SET = 0;
	i32 SEEK_END = 2;

	source resultSource;
	resultSource.name = fileName;

	rawptr fileHandle = fopen(string_to_cstring(fileName), "rb");
	if (fileHandle == nullptr)
		return resultSource;

	fseek(fileHandle, 0, SEEK_END);
	i64 fileSize = ftell(fileHandle);
	fseek(fileHandle, 0, SEEK_SET);

	u8[*] fileBuffer = malloc(cast(uint) fileSize);
	memset(fileBuffer, 0, cast(uint) fileSize);

	uint totalReadCount = fread(fileBuffer, cast(uint) fileSize, 1, fileHandle);
	if (totalReadCount != cast(uint) fileSize)
	{
		if (ferror(fileHandle) != 0)
		{
			perror("the following error occurred while reading source file:");
			fclose(fileHandle);
			return resultSource;
		}
	}

	fclose(fileHandle);

	string sourceText = fileBuffer[:cast(uint) fileSize];
	resultSource.text = sourceText;
	resultSource.is_valid = true;

	return resultSource;
}

string source_location_name_get(source_location sl) { return sl.source.name; }
uint source_location_line_get(source_location sl) { return sl.lineNumber; }
uint source_location_column_get(source_location sl) { return sl.columnNumber; }

string source_location_to_string(source_location sl)
{
	string slName = source_location_name_get(sl);

	uint lineDigitCount = uint_num_digits(source_location_line_get(sl));
	uint colDigitCount = uint_num_digits(source_location_column_get(sl));

	string_builder sb;
	string_builder_ensure_capacity(&sb, slName.length + lineDigitCount + colDigitCount + 2);
	string_builder_append_string(&sb, slName);
	string_builder_append_string(&sb, ":");
	string_builder_append_uint(&sb, source_location_line_get(sl));
	string_builder_append_string(&sb, ":");
	string_builder_append_uint(&sb, source_location_column_get(sl));

	return string_builder_to_string(sb);
}

string source_span_name_get(source_span ss) { return source_location_name_get(ss.startLocation); }

string source_span_to_string(source_span ss)
{
	return source_location_to_string(ss.startLocation);
}
