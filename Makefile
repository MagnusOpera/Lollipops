config ?= Debug
version ?= 0.0.0

build:
	dotnet build -c $(config)

test:
	dotnet run --project Tests/TestApp

nuget:
	dotnet pack -c $(config) /p:Version=$(version) -o out

publish: out/*.nupkg
	@for file in $^ ; do \
		dotnet nuget push $$file -k $(nugetkey) -s https://api.nuget.org/v3/index.json --skip-duplicate ; \
    done
