﻿[<AutoOpen>]
module Utils


let flip f x y = f y x


module String =

  let trim (s: string) =
    s.Trim ()

  /// Converts the first character to lowercase.
  let lowerFirst (s: string) =
    s.Substring(0, 1).ToLower() + s.Substring(1)

  /// Converts the first character to lowercase.
  let upperFirst (s: string) =
    s.Substring(0, 1).ToUpper() + s.Substring(1)

  /// Splits the string on the specified separator.
  let split (sep: string) (s: string) =
    s.Split(sep) |> Array.toList


module List =

  /// Returns the list after removing the first N elements. If the list has less
  /// than N elements, returns an empty list.
  let trySkip count list =
    if List.length list < count then [] else List.skip count list

  let trimEmptyLines list =
    list
    |> List.skipWhile ((=) "")
    |> List.rev
    |> List.skipWhile ((=) "")
    |> List.rev
