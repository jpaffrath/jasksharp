<p>
    <a href="https://github.com/jpaffrath/jasksharp/releases">
        <img src="https://img.shields.io/badge/version-2.0.0-green.svg"
             alt="Version Badge">
    </a>
    <a href="https://www.gnu.org/licenses/gpl-3.0.html">
        <img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg"
             alt="License Badge">
    </a>
    <a href="https://java.com/">
        <img src="https://img.shields.io/badge/required-.NET--10-purple.svg"
             alt="Java Badge">
    </a>
</p>

# Introduction

jask is a highly readable interpreter language in early development. The jask interpreter is fully written in C# without other dependencies. It is just a hobby project for fun and learning. Contributions are always welcome!

Seeing jask for the the first time? Try the [Getting strated guide](https://github.com/jpaffrath/jasksharp/wiki/Getting-started)! For further information, please visit the [Wiki](https://github.com/jpaffrath/jasksharp/wiki).

This is a reimplementation of the [jpaffrath/jask](https://github.com/jpaffrath/jask) interpreter, making this version 2 of the jask language.
I decided to rewrite the entire interpreter because the first version of jask was non-intuitive, very limited and I wanted to dig deeper into learning C# and the dotnet framework.

# Examples

## Hello World
```pseudo
set hello to "Hello, World!"
print(hello)
```
jask aims to be highly readable, easy to maintain, and understandable to beginners. The syntax largely avoids complex notation and is modeled after natural language.

## Functions, conditions, lists, loops and structs...
A more complex example for a direct deep-dive:
```pseudo
struct Edible
    set name to ""
    set number to 0
    set healthy to false
endstruct

function shouldIEatThis(edible: Edible)
    print(edible->number + "x " + edible->name + "? ")

    if edible->healthy == true
        print("Yes, absolutely!")
    else
        print("Oh no, I'd rather not")
    endif

    print("\n")
end

set apple to Edible(name = "Apple", number = 2, healthy = true)
set pizza to Edible(name = "Pizza", number = 5)

set myEdibles to list(apple, pizza)

for edible in myEdibles
    shouldIEatThis(edible)
    
endfor
```
This will output
```pseudo
2x Apple? Yes, absolutely!
5x Pizza? Oh no, I'd rather not
```

# Exectution
To use the interactive mode, invoke jask:
```terminal
dotnet run
```
Exit the interactive mode with the exit command.
jask can interpret files:
```pseudo
dotnet run examples/simple.jask
```
