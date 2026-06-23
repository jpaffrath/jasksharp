# Introduction

This is a reimplementation of the jpaffrath/jask interpreter in C#, making this version 2 of the jask language.
I decided to rewrite the entire interpreter because the first version of jask had some non-intuitive and annoying "features" and I wanted to dig deeper into learning C# and the dotnet framework.

This implementation uses the platform indipendend dotnet framework version 10 and has no dependencies to other libraries.

# Changes to jask 1.0.0

## Expression assign was removed
New and already existing variables can now be assigned with store
```Assembly
store 100 to myNum
store "Test" to myNum
```

## Arithmetic was simplified
In jask version 1.0.0, one has to use plus, minus, divide, etc. for simple arithmetic calculations.
This has been changed vor a better readability:
```Assembly
store 100 * 2.123 in myNum
store myNum / myNum in result
```

# Use
To use the interactive mode, invoke jask:
```Assembly
dotnet run
```
Exit the interactive mode with the exit command.
jask can interpret files:
```Assembly
dotnet run examples/simple.jask
```