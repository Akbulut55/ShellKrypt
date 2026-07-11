using System.Text;

namespace ShellKrypt.Infrastructure.Vaulting;

internal static partial class VaultCsvImportParser
{
    private static List<List<string>> ParseRecords(string csvText)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void Append(char value)
        {
            if (field.Length >= MaxCsvFieldChars)
                throw new InvalidDataException($"CSV field exceeds the {MaxCsvFieldChars} character limit.");

            field.Append(value);
        }

        void AddField()
        {
            if (row.Count >= MaxCsvColumns)
                throw new InvalidDataException($"CSV rows cannot exceed {MaxCsvColumns} columns.");

            row.Add(field.ToString());
            field.Clear();
        }

        void AddRow()
        {
            if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (records.Count >= MaxCsvRows + 1)
                    throw new InvalidDataException($"CSV import cannot exceed {MaxCsvRows} data rows.");

                records.Add(row.ToList());
            }

            row.Clear();
        }

        for (var i = 0; i < csvText.Length; i++)
        {
            var ch = csvText[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    AddField();
                    break;
                case '\r':
                    AddField();
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\n')
                        i++;
                    AddRow();
                    break;
                case '\n':
                    AddField();
                    AddRow();
                    break;
                default:
                    Append(ch);
                    break;
            }
        }

        if (inQuotes)
            throw new InvalidDataException("CSV contains an unterminated quoted field.");

        AddField();
        AddRow();

        return records;
    }
}
