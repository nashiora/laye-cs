
struct source_location
{
	string sourceName;
	uint characterIndex;

	uint lineNumber;
	uint columnNumber;
}

struct source_span
{
	source_location startLocation;
	source_location endLocation;
}

string source_location_name_get(source_location sl) { return sl.sourceName; }
uint source_location_line_get(source_location sl) { return sl.lineNumber; }
uint source_location_column_get(source_location sl) { return sl.columnNumber; }

string source_location_to_string(source_location sl)
{
	string slName = source_location_name_get(sl);

	string_builder sb;
	// most files will have less than 10k lines, so 5 + 5 + 2 (for :XXXXX:YYYYY) is a reasonable guess without doing real math for now
	string_builder_ensure_capacity(&sb, slName.length + 12);
	string_builder_append_string(&sb, slName);
	string_builder_append_string(&sb, ":");
	string_builder_append_uint(&sb, source_location_line_get(sl));
	string_builder_append_string(&sb, ":");
	string_builder_append_uint(&sb, source_location_column_get(sl));

	return string_builder_to_string(sb);
}

string source_span_name_get(source_span ss) { return ss.startLocation.sourceName; }

string source_span_to_string(source_span ss)
{
	return source_location_to_string(ss.startLocation);
}
