test:
	dotnet build src/InspecTree.sln
	dotnet test src/InspecTree.sln

lint:
	dotnet format style --verify-no-changes src/InspecTree.sln
	dotnet format analyzers --verify-no-changes src/InspecTree.sln

pack: 
	dotnet pack src/InspecTree/InspecTree.csproj -c Release -o ./nupkg -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg

example:
	dotnet run --project src/InspecTree.Example/InspecTree.Example.csproj