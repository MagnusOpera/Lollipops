config ?= Debug
version ?= 0.0.0


build:
	dotnet build -c $(config)

dist:
	dotnet pack -c $(config) /p:Version=$(version) -o .out

test: dist
	dotnet run -c $(config) --project Tests/TestApp

publish: .out/MagnusOpera.Lollipops.*.nupkg
	@for file in $^ ; do \
		dotnet nuget push $$file -k $(nugetkey) -s https://api.nuget.org/v3/index.json --skip-duplicate ; \
    done
