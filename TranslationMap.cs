class TranslationData
{
    public string Source { get; set; }    
    public string Translation { get; set; }     
}
class TranslationItem
{
    public string ResId { get; set; }
    public string DefaultSource { get; set; } 
    public Dictionary<string,TranslationData> Translations { get; set; } = new Dictionary<string, TranslationData>();
    public string DefaultValue {get; set;}
}

class TranslationMap{
    public DateTime CreatedUtc { get; set; }
    public Dictionary<string,TranslationItem> Maps = new();
}
