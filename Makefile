ifeq ($(OS),Windows_NT)
	SHELL := pwsh.exe
	.SHELLFLAGS := -NoProfile -Command
endif

test: build
	dotnet test src/InspecTree.sln

lint:
	dotnet format style --verify-no-changes src/InspecTree.sln
	dotnet format analyzers --verify-no-changes src/InspecTree.sln

pack: nupkg/InspecTree.0.0.0.nupkg

example: nupkg/InspecTree.0.0.0.nupkg
	dotnet run --project src/InspecTree.Example/InspecTree.Example.csproj

nupkg/InspecTree.0.0.0.nupkg: build
	dotnet pack src/InspecTree/InspecTree.csproj -c Release -o ./nupkg -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -p:Version=0.0.0
	dotnet nuget locals all --clear

build:
	dotnet build src/InspecTree.sln