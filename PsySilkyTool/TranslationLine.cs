using System.Collections.Generic;

public class TranslationLine
{
    public List<string> ReferenceLines { get; private set; } = new List<string>();
    public List<string> TranslatedLines { get; set; } = new List<string>();

    public string ReferenceLine => string.Join("", ReferenceLines);
    public string TranslatedLine => string.Join(" ", TranslatedLines);

    public override string ToString()
    {
        return $"{ReferenceLine} / {TranslatedLine}";
    }
}
