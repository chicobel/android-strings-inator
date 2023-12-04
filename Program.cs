// See https://aka.ms/new-console-template for more information
using System.Xml;
using CLAP.Validation;
using Newtonsoft.Json;

string? rootFolder=null, mode = null,mapFile = "translations.json", outputFolder = null, translationsFolder = null;
List<string> StringXmls = new();
List<string> IgnoreList = new() { "build",".gradle","proguard","schemas","gradle","locale","java","assets"};
TranslationMap translationMap;
Dictionary<string,TranslationItem> maps = new();
//stores the available translation and where they need to go
Dictionary<string,string> AvailableTranslations = new();
if(args.Length >0)
{
    ParseInputCommands(args, ref rootFolder, ref mode, ref mapFile, ref outputFolder, ref translationsFolder);
    if(mode == "combine" && (string.IsNullOrEmpty(rootFolder) || string.IsNullOrEmpty(mode) || outputFolder == null))
    {
        Environment.Exit(Environment.ExitCode);
    }

    rootFolder = Path.Combine(Environment.CurrentDirectory, rootFolder ?? "");
    if (!string.IsNullOrEmpty(mapFile) && File.Exists(Path.Combine(Environment.CurrentDirectory, mapFile)))
    {
        Console.WriteLine("Loading translation map");
    }
    else
    {
        Console.WriteLine("No map file exsists, please specify one now if it exists or don't come crying to me later! \n Press enter to continue if none exist, one will be created");
        mapFile = ""; //Console.ReadLine();
        mapFile = mapFile?.Trim();
        if (string.IsNullOrEmpty(mapFile) || !File.Exists(Path.Combine(Environment.CurrentDirectory, mapFile)))
        {
            Console.WriteLine("A new map will be generated..");
            if(string.IsNullOrEmpty(mapFile))
                mapFile = "translations.json";
            else if(mapFile.Contains('.') && !mapFile.EndsWith(".json")){
                mapFile = mapFile.Split(".")[0]+".json";
            } else {
                mapFile += ".json";
            }
        }
    }
 
   if(mode == "combine"){
        Console.WriteLine("Locating string resources..");
        GetStringXmls(rootFolder);
        Console.WriteLine("Generating map from default language");
        LoadstringsFromFiles(true);//we only want to do the default language here and produce a combined strings.xml file   
        Console.WriteLine("Storing map in {0}",mapFile);
        translationMap = new TranslationMap { CreatedUtc = DateTime.UtcNow, Maps = maps };
        JsonSerializer serializer = new JsonSerializer();
        serializer.NullValueHandling = NullValueHandling.Ignore;
        serializer.Formatting = Newtonsoft.Json.Formatting.Indented;

        using (StreamWriter sw = new StreamWriter(Path.Combine(Environment.CurrentDirectory, mapFile)))
        using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, translationMap);
        }
        Console.WriteLine("Successfully stored map file map in {0}",mapFile);
        //write out combined stings.xml
        WriteMergergedStringsFile();             
        Environment.Exit(0);
        return;
    }
    if (string.IsNullOrEmpty(mapFile) || !File.Exists(Path.Combine(Environment.CurrentDirectory, mapFile)))
    {
        Console.WriteLine("Exiting, no map file found");
        Environment.Exit(-1);
    }

    //load the map into memory
    try{
        var tempMap = JsonConvert.DeserializeObject<TranslationMap>(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, mapFile)));
        if(tempMap != null){
            translationMap = tempMap;
            maps = translationMap.Maps;
        }
    
    } catch(Exception e){
        Console.WriteLine("A problem occured loading the translation map..");
        Console.Write(e);
        Environment.Exit(-1);
    }
    if(string.IsNullOrEmpty(translationsFolder) || !Directory.Exists(translationsFolder)){
        Console.WriteLine("Exiting, no map file found");
        Environment.Exit(-1);
    }
    var translationsFolderPath = Path.Combine(Environment.CurrentDirectory, translationsFolder);
    
    //Locate all translated string
    GetStringXmls(translationsFolderPath);
    //Extract the tranlations into memory
    LoadstringsFromFiles();
    //Write the separate translated strings to their various destinations
    WriteTranslatedStrings();
}
else
{
   ShowErrorAndHelp();
   Environment.Exit(-1);
}

void WriteMergergedStringsFile(){

 using var fs = new StreamWriter(Path.Combine(Environment.CurrentDirectory, outputFolder,"strings.xml"), new FileStreamOptions{ Mode= FileMode.Create, Share= FileShare.ReadWrite, Access = FileAccess.Write});
    fs.WriteLine("<resources>");
        foreach(var t in maps){
            fs.WriteLine($"    <!--{t.Value.DefaultSource}-->");//preceed each line with some hints of where it belongs
            fs.WriteLine($"    <string name=\"{t.Key}\">{t.Value.DefaultValue}</string>");
        }
    fs.WriteLine("</resources>");
}

//Write the translated string to their various locations
void WriteTranslatedStrings(){
    var groupedOutputFiles = maps.Values.Select(p =>p.DefaultSource).Distinct();

    foreach(var localeFile in groupedOutputFiles){
        foreach(var language in AvailableTranslations){
            StreamWriter? fileStream = null;
            var outputPathh = Path.Combine(Environment.CurrentDirectory,localeFile.Replace("values","values-"+language.Key));
            var outputValuesFolder = outputPathh.Replace("strings.xml","");
            if(!Directory.Exists(outputValuesFolder))
                Directory.CreateDirectory(outputValuesFolder);
            
            try{
                fileStream = new StreamWriter(outputPathh, new FileStreamOptions{ Mode= FileMode.Create, Share = FileShare.ReadWrite, Access = FileAccess.ReadWrite});
                fileStream.WriteLine("<resources>");
                foreach(var t in maps){
                    if(t.Value.DefaultSource != localeFile)//does not belong here
                        continue;
                var translationForLocale = t.Value.Translations[language.Key];
                fileStream.WriteLine($"    <string name=\"{t.Value.ResId}\">{translationForLocale.Translation}</string>");
            }
            fileStream?.WriteLine("</resources>");
            }catch (Exception e){
                Console.WriteLine("Failed to write translated string to: {0}",outputPathh);
            } finally {
                fileStream?.Close();
            }
        }
    }

}

bool isDefaultStringXml(string path) => path.EndsWith(Path.Combine("values", "strings.xml"));

void ShowErrorAndHelp(){
     Console.WriteLine(@"No arguments specified
    Please specify a root folder using -root
    Please specify a mode using -mode options include localise, compile
    ");
}

string? extractTwoLetterLocale(string data){
    var split = data.Split("values-");
    if(split.Length <2) return null;
    var divider = data.IndexOf("/") > -1 ? "/" : "\\";
    split = split[1].Split(divider);
    return split[0];
}

void LoadstringsFromFiles(bool defaultsOnly = false){
    foreach(var f in StringXmls){
        var isDefaultString = isDefaultStringXml(f);
        if(defaultsOnly && !isDefaultString)
            continue;
        var locale = extractTwoLetterLocale(f);
        if(!isDefaultString){
            if(locale != null && !AvailableTranslations.ContainsKey(locale))
                AvailableTranslations[locale] = f;
        }
        var doc = new XmlDocument();
        doc.Load(f);
        XmlNode? root = doc.DocumentElement; 
        if(root == null){
            Console.WriteLine("Failed to parse xml at {0}",f);
            Environment.Exit(-1);
            break;
        }
        var nodes = root.SelectNodes("string");
        if(nodes == null){
            Console.WriteLine("Failed to locate string elements in {0}",f);
            Environment.Exit(-1);
            break;
        }
        foreach(XmlNode node in nodes){
            var resid  = node?.Attributes?["name"]?.Value;
            
            if(string.IsNullOrEmpty(resid)){
                Console.WriteLine("Failed to extract Resource Identifier in {0}",f);
                break;
            }
            var key = resid;
            if(mode == "combine") 
            {
                key = f.Replace(Environment.CurrentDirectory,"")+"||"+resid;
                key = CreateMD5(key);
            }
            if(!maps.TryGetValue(key, out TranslationItem? value))
            {
                value = new TranslationItem{
                        ResId =resid,
                };
                maps[key] = value;
            }
            if(isDefaultString && mode == "combine"){
                value.DefaultSource = f.Replace(Environment.CurrentDirectory+Path.DirectorySeparatorChar,"");
                value.DefaultValue = node?.InnerText ?? "";
            } else {
                if(locale == null)
                    continue;
                var exists = value.Translations.ContainsKey(locale);
                if(!exists)
                    value.Translations[locale] = new TranslationData();
                value.Translations[locale].Source = value.DefaultSource .Replace(Path.DirectorySeparatorChar+"values",Path.DirectorySeparatorChar+"values-"+locale);
                value.Translations[locale].Translation = node?.InnerText ?? "";
            }
        }
    }
}

void GetStringXmls(string directory){
    var dir = Directory.EnumerateFiles(directory);
   
    foreach(var f in dir){
        if(Path.GetFileName(f) == "strings.xml"){
            #if DEBUG
            Console.WriteLine("Located string resource at: "+Path.GetDirectoryName(f));
            #endif
            StringXmls.Add(f);
        }
    }
    //check subdirs
     var subDirs = Directory.EnumerateDirectories(directory);
     if(subDirs.Any())
     {
        foreach(var dirs in subDirs){
            //filter out build directories etc
            if(IgnoreList.Any(p=>p.Equals(Path.GetDirectoryName(dirs), StringComparison.InvariantCultureIgnoreCase))){
                #if DEBUG
                Console.WriteLine("Ignoring Directory:"+dirs);
                #endif
                continue;
            }
            GetStringXmls(dirs);
        }
     }
}

static void ParseInputCommands(string[] args, ref string? rootFolder, ref string? mode, ref string? mapFile, ref string? outputFolder, ref string? translationsFolder)
{
    for (var i = 0; i < args.Length; i++)
    {
        var previousArg = i >= 1 ? args[i - 1] : "";
        var arg = args[i];
#if DEBUG
        Console.WriteLine($"Argument={arg}");
#endif
        if (previousArg == "-mode")
            mode = arg.Trim();
        if (previousArg == "-root")
            rootFolder = arg.Trim();
        if (previousArg == "-map")
            mapFile = arg.Trim();
        if(previousArg == "-output")
            outputFolder = arg.Trim();
        if(previousArg == "-translations")
            translationsFolder = arg.Trim();
    }
}

// static string Base64Encode(string plainText) 
// {
//   var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
//   return System.Convert.ToBase64String(plainTextBytes);
// }

// static string Base64Decode(string base64EncodedData) 
// {
//   var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
//   return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
// }
static string CreateMD5(string input)
{
    // Use input string to calculate MD5 hash
    using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
    {
        byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        return Convert.ToHexString(hashBytes); // .NET 5 +
    }
}