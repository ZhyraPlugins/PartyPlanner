.PHONY: debug release clean

debug:
	dotnet build -c Debug

release:
	dotnet build -c Release

clean:
	dotnet clean
