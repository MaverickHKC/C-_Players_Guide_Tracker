\# C# Player's Guide — XP Tracker



A progress tracker for the \[C# Player's Guide 5th Edition](https://csharpplayersguide.com).



Tracks all 114 challenges across all 5 parts of the book with XP totals,

level progression, category filtering, and persistent save per user.



\## Download



Grab the latest `XpTracker.exe` from the \[Releases](../../releases) page.

No installation required — just download and run.



\## Building From Source



Requires the \[.NET SDK](https://dotnet.microsoft.com/download).

```bash

dotnet run

```



To publish a standalone executable:

```bash

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

```

\## Note on Windows SmartScreen
Windows may flag this as an unknown app on first run. 
Click "More info" → "Run anyway" to proceed. 
This is normal for small open source tools without a code signing certificate.
