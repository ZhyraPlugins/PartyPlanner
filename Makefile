.PHONY: debug release test clean

debug:
	dotnet build -c Debug

release:
	dotnet build -c Release

test:
	dotnet test PartyPlanner.Tests

clean:
	dotnet clean
