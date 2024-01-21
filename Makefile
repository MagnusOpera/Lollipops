config ?= Debug
version ?= 0.0.0

build:
	dotnet build -c $(config)

nuget:
	dotnet pack -c $(config) /p:Version=$(version) -o .nugets

test: nuget
	dotnet run -c $(config) --project Tests/TestApp

publish: nuget
	dotnet nuget push .nugets/MagnusOpera.Lollipops.$(version).nupkg -k $(nugetkey) -s https://api.nuget.org/v3/index.json --skip-duplicate
