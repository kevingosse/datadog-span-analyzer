# datadog-span-analyzer

Either build from Visual Studio (SpanAnalyzer.csproj) or from the command-line: 

```
dotnet build -r win-x64
```

Then launch the executable with a path to a memory dump:

```
SpanAnalyzer.exe C:\dumps\myMemoryDump.dmp
```

The result is printed into the console, so it's recommended to redirect it to a file:

```
SpanAnalyzer.exe C:\dumps\myMemoryDump.dmp > output.txt
```

SpanAnalyzer must be compiled with the same bitness as the target memory dump. So to open a 32 bit memory dump, the tool must be compiled in 32 bit:

```
dotnet build -r win-x86
```
