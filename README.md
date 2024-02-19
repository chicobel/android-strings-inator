# android-strings-inator
Light-weight console app to combine &amp; expand android resource strings in modularised apps.

There are 2 modes, compress & expand.

Compress is used to recurse through all of the folders at the specifid path and merge the contents of all strings.xml files into
a single strings.xml file that than be sent off to a translator. This is the default mode if none spectified.

Expand, this mode is used when the differen value/strings.xml files have come back from the translatorts and we wan to place them back into the projectects/folders where they belong.

# Example compress

dotnet run -root "../../" -mode combine -output " " 

-Note if the ouptut path is empty, the strings.xml file is written to the same path as the current directory from which the comman is run.

# Example expand

dotnet run -translations "/Users/{user}/Downloads/Android-tav-translations-strings" -mode expand
