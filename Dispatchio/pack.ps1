dotnet restore /nowarn:netsdk1138
dotnet clean
dotnet build -c Release
dotnet pack src/Dispatchio/Dispatchio.csproj --no-restore --no-build -c Release -o ./artifacts
