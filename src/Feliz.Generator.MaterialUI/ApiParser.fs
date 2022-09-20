﻿module ApiParser

open System
open System.Collections.Generic
open FSharp.Data
open Feliz.Generator
open System.Text.RegularExpressions
open Domain


let parseClassRule (row: ComponentApiPage.Css.Row) (rowHtml: HtmlNode) =
    let markdownDocLines =
        rowHtml.CssSelect("td").[2].Elements()
        |> docElementsToMarkdownLines

    let methodName =
        row.``Rule name``
        |> kebabCaseToCamelCase
        |> trimStart '@'
        |> appendApostropheToReservedKeywords

    if methodName = "" then
        // Classes API may not be supported for this component
        // https://github.com/mui-org/material-ui/issues/22783#issuecomment-699686571
        None
    else
        Some
            { DocLines = markdownDocLines
              MethodName =
                row.``Rule name``
                |> kebabCaseToCamelCase
                |> trimStart '@'
                |> appendApostropheToReservedKeywords
              RealRuleName = row.``Rule name``
              GlobalClass = row.``Global class`` }

let parseProp componentMethodName (row: ComponentApiPage.Props.Row) (rowHtml: HtmlNode) : Prop =
    let isRequired = row.Name.EndsWith "*"
    let realPropName = row.Name.Trim(' ', '*')

    let propMethodName =
        realPropName
        |> kebabCaseToCamelCase
        |> appendApostropheToReservedKeywords

    let markdownDocLines =
        if propMethodName = "classes" then
            [ "Override or extend the styles applied to the component." ]
        else
            rowHtml.CssSelect("td").[3].Elements()
            |> docElementsToMarkdownLines

    let propDocType = row.Type.Trim()

    let isProbablyEnumProp =
        //(componentMethodName = "grid" && propMethodName = "spacing")
        (propDocType.Contains "'" // There is a string value
         && propDocType <> "'' | any"
         && not (propDocType.Contains "Array<")
         && not (
             componentMethodName = "slider"
             && propMethodName = "componentsProps"
         )
         && not (
             componentMethodName = "popper"
             && [ "modifiers"; "popperOptions" ]
                |> List.contains propMethodName
         )
         && (propDocType.Contains "|" // There are multiple values (at least one string per above)
             || (propDocType.StartsWith "'"
                 && propDocType.EndsWith "'"))) // There is a single string value

    let (|AutoComplelete|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | "autocomplete", "isOptionEqualToValue", "func" ->
            Some [ RegularPropOverload.create "(tester: ('a -> 'a -> bool))" "tester" ]

        | "autocomplete", "filterOptions", "func" ->
            Some [ RegularPropOverload.create
                       "(filterOptions: 'option [] -> string -> 'option [])"
                       "(Func<_,_,_> (fun opts (s: AutocompleteFilterOptionsState) -> ResizeArray (filterOptions opts s.inputValue)))"
                   RegularPropOverload.create
                       "(filterOptions: 'option [] -> AutocompleteFilterOptionsState -> 'option [])"
                       "(Func<_,_,_> filterOptions)" ]

        | "autocomplete", "getOptionDisabled", "func" ->
            Some [ RegularPropOverload.create "(isDisabled: 'option -> bool)" "isDisabled" ]

        | "autocomplete", "getOptionLabel", "func" ->
            Some [ RegularPropOverload.create "(getLabel: 'option -> string)" "getLabel" ]

        | "autocomplete", "getOptionSelected", "func" ->
            Some [ RegularPropOverload.create "(getSelected: 'option -> 'a -> bool)" "(Func<_,_,_> getSelected)" ]

        | "autocomplete", "getLimitTagsText", "func" ->
            Some [ RegularPropOverload.create "(getText: int -> ReactElement)" "getText" ]

        | "autocomplete", "groupBy", "func" ->
            Some [ RegularPropOverload.create "(getGroup: 'option -> string)" "getGroup" ]

        | "autocomplete", "onChange", "func" ->
            Some [ RegularPropOverload.create
                       "(handler: Event -> 'option -> AutocompleteOnChangeReason -> unit)"
                       "(Func<_,_,_,_> handler)"
                   |> RegularPropOverload.setExtension true
                   RegularPropOverload.create "(handler: 'option -> unit)" "(Func<_,_,_,_> (fun _ v _ -> handler v))"
                   |> RegularPropOverload.setExtension true

                   RegularPropOverload.create
                       "(handler: Event -> 'option [] -> AutocompleteOnChangeReason -> unit)"
                       "(Func<_,_,_,_> handler)"
                   RegularPropOverload.create "(handler: 'option [] -> unit)" "(Func<_,_,_,_> (fun _ v _ -> handler v))"

                   RegularPropOverload.create
                       "(handler: Event -> 'option option -> AutocompleteOnChangeReason -> unit)"
                       "(Func<_,_,_,_> handler)"
                   RegularPropOverload.create
                       "(handler: 'option option -> unit)"
                       "(Func<_,_,_,_> (fun _ v _ -> handler v))" ]

        | "autocomplete", "onInputChange", "func" ->
            Some [ RegularPropOverload.create
                       "(handler: Event -> string -> AutocompleteInputChangeReason -> unit)"
                       "(Func<_,_,_,_> handler)"
                   RegularPropOverload.create "(handler: string -> unit)" "(Func<_,_,_,_> (fun _ v _ -> handler v))" ]

        | "autocomplete", "onHighlightChange", "func" ->
            Some [ RegularPropOverload.create
                       "(handler: Event -> 'option -> AutocompleteHighlightChangeReason -> unit)"
                       "(Func<_,_,_,_> handler)"
                   RegularPropOverload.create
                       "(handler: 'option -> AutocompleteHighlightChangeReason -> unit)"
                       "(Func<_,_,_,_> (fun _ o r -> handler o r))"
                   RegularPropOverload.create "(handler: 'option -> unit)" "(Func<_,_,_,_> (fun _ o _ -> handler o))" ]

        | "autocomplete", "options", "array" ->
            Some [ RegularPropOverload.create "(options: 'option [])" "(ResizeArray options)" ]

        | "autocomplete", "renderGroup", "func" ->
            Some [ RegularPropOverload.create "(render: AutocompleteRenderGroupParams -> ReactElement)" "render" ]

        | "autocomplete", "renderInput", "func" ->
            Some [ RegularPropOverload.create "(render: AutocompleteRenderInputParams -> ReactElement)" "render" ]

        | "autocomplete", "renderOption", "func" ->
            Some [ RegularPropOverload.create
                       "(render: 'option -> AutocompleteRenderOptionState -> ReactElement)"
                       "(Func<_,_,_> render)" ]

        | "autocomplete", "renderTags", "func" ->
            Some [ RegularPropOverload.create
                       "(render: 'option [] -> AutocompleteRenderValueState -> ReactElement)"
                       "(Func<_,_,_> render)" ]

        | "autocomplete",
          ("defaultValue"
          | "value"),
          "any" ->
            Some [ RegularPropOverload.create "(value: 'option [])" "(ResizeArray value)"
                   RegularPropOverload.create "(value: 'option option)" "value"
                   RegularPropOverload.create "(value: 'option)" "value"
                   |> RegularPropOverload.setExtension true ]

        | _ -> None

    let (|AllDateTimePickers|_|) componentMethodName =
        match componentMethodName with
        | "dateTimePicker"
        | "desktopDateTimePicker"
        | "mobileDateTimePicker"
        | "staticDateTimePicker" -> Some componentMethodName
        | _ -> None

    let (|AllDatePickers|_|) componentMethodName =
        match componentMethodName with
        | "datePicker"
        | "desktopDatePicker"
        | "mobileDatePicker"
        | "staticDatePicker" -> Some componentMethodName
        | _ -> None

    let (|AllTimePickers|_|) componentMethodName =
        match componentMethodName with
        | "timePicker"
        | "desktopTimePicker"
        | "mobileTimePicker"
        | "staticTimePicker" -> Some componentMethodName
        | _ -> None

    let (|AllDateRangePickers|_|) componentMethodName =
        match componentMethodName with
        | "dateRangePicker"
        | "desktopDateRangePicker"
        | "mobileDateRangePicker"
        | "staticDateRangePicker" -> Some componentMethodName
        | _ -> None

    let (|DateAndTimePickers|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | (AllDatePickers _
          | "calendarPicker"),
          "views",
          "Array<'day' | 'month' | 'year'>" ->
            Some [ RegularPropOverload.create "([<ParamArray>] views: DatePickerView [])" "views" ]

        | (AllTimePickers _
          | "clockPicker"),
          "views",
          "Array<'hours' | 'minutes' | 'seconds'>" ->
            Some [ RegularPropOverload.create "([<ParamArray>] views: TimePickerView [])" "views" ]

        | AllDateTimePickers _, "views", "Array<'day' | 'hours' | 'minutes' | 'month' | 'seconds' | 'year'>" ->
            Some [ RegularPropOverload.create "([<ParamArray>] views: DateTimePickerView [])" "views" ]

        | (AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _
          | "calendarPicker"),
          "shouldDisableDate",
          "func" ->
            Some [ RegularPropOverload.create
                       "(shouldDisableDate: System.DateTime -> bool)"
                       "(Func<_, _> shouldDisableDate)" ]

        | (AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _
          | "calendarPicker"
          | "yearPicker"),
          "shouldDisableYear",
          "func" ->
            Some [ RegularPropOverload.create "(shouldDisableYear: int -> bool)" "(Func<_, _> shouldDisableYear)" ]

        | (AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _
          | "calendarPicker"
          | "monthPicker"),
          "shouldDisableMonth",
          "func" ->
            Some [ RegularPropOverload.create "(shouldDisableMonth: int -> bool)" "(Func<_, _> shouldDisableMonth)" ]

        | (AllDateTimePickers _
          | AllDatePickers _),
          "value",
          "any | Date | number | string" ->
            Some [ RegularPropOverload.create "(value: System.DateTime)" "value"
                   RegularPropOverload.create "(value: string)" "value"
                   RegularPropOverload.create "(value: int)" "value" ]

        | (AllDateRangePickers _),
          "value",
          ("Array<any | Date | number | string>"
          | "Array<any>") ->
            Some [ RegularPropOverload.create "(value: System.DateTime list)" "value"
                   RegularPropOverload.create "(value: string list)" "value"
                   RegularPropOverload.create "(value: int list)" "value" ]

        | (AllDateTimePickers _
          | AllTimePickers _
          | "clockPicker"),
          "getClockLabelText",
          "func"
        | (AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _
          | AllTimePickers _),
          "rifmFormatter",
          "func"
        | ("calendarPicker"
          | AllDateTimePickers _
          | AllDateRangePickers _
          | AllTimePickers _
          | AllDatePickers _),
          ("getOpenDialogAriaText"
          | "getViewSwitchingButtonText"),
          "func" -> Some []

        | "clockPicker",
          ("getHoursClockNumberText"
          | "getMinutesClockNumberText"
          | "getSecondsClockNumberText" as name),
          "func" -> Some [ RegularPropOverload.create (sprintf "(%s: string -> string)" name) name ]

        | (AllDateTimePickers _
          | AllTimePickers _
          | "clockPicker"),
          "shouldDisableTime",
          "func" ->
            Some [ RegularPropOverload.create
                       "(shouldDisableTime: System.DateTime -> string -> bool)"
                       "(Func<_, _, _> shouldDisableTime)" ]

        | AllDateRangePickers _, "calendars", "1 | 2 | 3" ->
            Some [ RegularPropOverload.create "(numCalendars: int)" "numCalendars" ]

        | (AllDateTimePickers _
          | AllDateRangePickers _
          | AllDatePickers _
          | AllTimePickers _),
          "acceptRegex",
          "RegExp" ->
            Some [ RegularPropOverload.create "(acceptRegex: System.Text.RegularExpressions.Regex)" "acceptRegex" ]

        | ("calendarPicker"
          | AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _),
          "dayOfWeekFormatter",
          "func" -> Some [ RegularPropOverload.create "(formatter: string -> string)" "(Func<_, _> formatter)" ]

        | _ -> None

    let (|LocalizationProviderRegularOverloads|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | "localizationProvider", "dateAdapter", "func" -> Some []

        | "localizationProvider", "locale", "object | string" -> Some []

        | "localizationProvider", "adapterLocale", "object | string" ->
            Some [ RegularPropOverload.create "(localeCode: string)" "localeCode" ]

        | "localizationProvider", "dateFormats", formats ->
            let formatsObjEntries = formats.TrimStart('{').TrimEnd('}').Split(',')

            let formatsKeyValuePairs =
                [ for entry in formatsObjEntries do
                      match entry.Trim().Split(':') with
                      | [| objKey; objValue |] ->
                          let isOptional = objKey.EndsWith('?')
                          let propName = objKey.Trim().TrimEnd('?')

                          if objValue.Trim() = "string" then
                              yield (propName, "string", isOptional)
                          else
                              yield! []

                      | _ -> yield! [] ]

            let formatsParams, formatsValue = formatsKeyValuePairs |> paramListAndObjCreator

            Some [ RegularPropOverload.create formatsParams formatsValue ]

        | "localizationProvider", "localeText", "object" -> Some [ RegularPropOverload.create "(value: obj)" "value" ]

        | "localizationProvider", "dateLibInstance", "any" -> Some [ RegularPropOverload.create "(value: obj)" "value" ]

        | _ -> None

    let (|Popover|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | "popover", "action", "ref" ->
            Some [ RegularPropOverload.create "(ref: IRefValue<PopoverActions option>)" "ref"
                   RegularPropOverload.create "(handler: PopoverActions -> unit)" "handler" ]

        | "popover",
          ("anchorOrigin"
          | "transformOrigin"),
          "{ horizontal: 'center' | 'left' | 'right' | number, vertical: 'bottom' | 'center' | 'top' | number }" ->
            Some [ RegularPropOverload.create
                       "(horizontal: PopoverOriginHorizontal, vertical: PopoverOriginVertical)"
                       "(createObj [ \"horizontal\" ==> horizontal; \"vertical\" ==> vertical ])"
                   RegularPropOverload.create
                       "(horizontal: int, vertical: PopoverOriginVertical)"
                       "(createObj [ \"horizontal\" ==> horizontal; \"vertical\" ==> vertical ])"
                   RegularPropOverload.create
                       "(horizontal: PopoverOriginHorizontal, vertical: int)"
                       "(createObj [ \"horizontal\" ==> horizontal; \"vertical\" ==> vertical ])"
                   RegularPropOverload.create
                       "(horizontal: int, vertical: int)"
                       "(createObj [ \"horizontal\" ==> horizontal; \"vertical\" ==> vertical ])" ]

        | "popover", "anchorPosition", "{ left: number, top: number }" ->
            Some [ [ "left", "int", false
                     "top", "int", false ]
                   |> paramListAndObjCreator
                   ||> RegularPropOverload.create ]

        | "popover", "onClose", "func" ->
            Some [ RegularPropOverload.create "(handler: Event -> PopoverCloseReason -> unit)" "(Func<_,_,_> handler)"
                   RegularPropOverload.create
                       "(handler: PopoverCloseReason -> unit)"
                       "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "popover", "getContentAnchorEl", "func" ->
            Some [ RegularPropOverload.create "(handler: #Element option -> unit)" "handler" ]

        | _ -> None

    let (|Slider|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | "slider", "componentsProps", type' ->
            Some [ RegularPropOverload.create "(componentsProps: obj)" "componentsProps"
                   RegularPropOverload.create "(componentsProps: seq<IReactProperty>)" "(createObj !!componentsProps)" ]
        | "slider",
          ("value"
          | "defaultValue"),
          _ ->
            Some [ RegularPropOverload.create "(value: int)" "value"
                   RegularPropOverload.create "(value: float)" "value"
                   RegularPropOverload.create "(min: int, max: int)" "(min, max)"
                   RegularPropOverload.create "(min: float, max: float)" "(min, max)" ]

        | "slider", "marks", "bool | array" ->
            Some [ RegularPropOverload.create "(value: bool)" "value"
                   RegularPropOverload.create
                       "([<ParamArray>] values: int [])"
                       "(values |> Array.map (fun x -> createObj [ \"value\" ==> x ]))"
                   RegularPropOverload.create
                       "([<ParamArray>] values: float [])"
                       "(values |> Array.map (fun x -> createObj [ \"value\" ==> x ]))"
                   RegularPropOverload.create
                       "([<ParamArray>] valuesAndLabels: (int * string option) [])"
                       "(valuesAndLabels |> Array.map (fun (v, lb) -> createObj [ \"value\" ==> v; \"label\" ==> lb ]))"
                   RegularPropOverload.create
                       "([<ParamArray>] valuesAndLabels: (float * string option) [])"
                       "(valuesAndLabels |> Array.map (fun (v, lb) -> createObj [ \"value\" ==> v; \"label\" ==> lb ]))" ]

        | "slider", "valueLabelFormat", _ ->
            Some [ RegularPropOverload.create "(value: string)" "value"
                   RegularPropOverload.create "(format: int -> int -> string)" "format"
                   RegularPropOverload.create "(format: float -> int -> string)" "format" ]

        | "slider", "getAriaLabel", "func" -> Some [ RegularPropOverload.create "(getLabel: int -> string)" "getLabel" ]

        | "slider", "getAriaValueText", "func" ->
            Some [ RegularPropOverload.create "(getText: int -> int -> string)" "getText"
                   RegularPropOverload.create "(getText: float -> int -> string)" "getText" ]

        | "slider", "scale", "func" ->
            Some [ RegularPropOverload.create "(transform: int -> int)" "transform"
                   RegularPropOverload.create "(transform: float -> float)" "transform" ]

        | "slider",
          ("onChange"
          | "onChangeCommitted"),
          "func" ->
            Some [ RegularPropOverload.create "(handler: Event -> int -> unit)" "(Func<_,_,_> handler)"
                   RegularPropOverload.create "(handler: Event -> float -> unit)" "(Func<_,_,_> handler)"
                   RegularPropOverload.create "(handler: int -> unit)" "(Func<_,_,_> (fun _ v -> handler v))"
                   RegularPropOverload.create "(handler: float -> unit)" "(Func<_,_,_> (fun _ v -> handler v))"
                   RegularPropOverload.create "(handler: Event -> int [] -> unit)" "(Func<_,_,_> handler)"
                   RegularPropOverload.create "(handler: Event -> float [] -> unit)" "(Func<_,_,_> handler)"
                   RegularPropOverload.create "(handler: int [] -> unit)" "(Func<_,_,_> (fun _ v -> handler v))"
                   RegularPropOverload.create "(handler: float [] -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "slider", "step", "number" ->
            Some [ RegularPropOverload.create "(value: int)" "value"
                   RegularPropOverload.create "(value: float)" "value"
                   RegularPropOverload.create "(value: int option)" "value"
                   RegularPropOverload.create "(value: float option)" "value"
                   |> RegularPropOverload.setExtension true ]

        | _ -> None

    let (|GridAndStack|_|) (componentMethodName, propMethodName, propDocType) =
        match componentMethodName, propMethodName, propDocType with
        | ("grid"
          | "stack"),
          ("spacing"
          | "rowSpacing"
          | "columnSpacing"),
          "Array<number | string> | number | object | string" ->
            Some [
                RegularPropOverload.create "(spacing: int)" "spacing"
                RegularPropOverload.create "(spacing: Styles.ICssUnit)" "spacing"
                RegularPropOverload.create "(spacing: string)" "spacing"
                //RegularPropOverload.create "([<ParamArray>] spacing: IBreakpointedProp<int> [])" "(createObj !!spacing)"
                //RegularPropOverload.create "([<ParamArray>] spacing: IBreakpointedProp<Styles.ICssUnit> [])" "(createObj !!spacing)"
                RegularPropOverload.createWithMuiBreakpoints "int"
                RegularPropOverload.createWithMuiBreakpoints "Styles.ICssUnit"
            ]

        | ("grid"
          | "stack"),
          "direction",
          _ ->
            Some [
                RegularPropOverload.create "(direction: FlexDirection)" "direction"
                RegularPropOverload.createWithMuiBreakpoints "FlexDirection"
                //RegularPropOverload.create "([<ParamArray>] direction: IBreakpointedProp<FlexDirection> [])" "(createObj !!direction)"
            ]

        | "grid", "columns", _ ->
            Some [
                RegularPropOverload.create "(value: int)" "value"
                RegularPropOverload.createWithMuiBreakpoints "int"
                //RegularPropOverload.create "([<ParamArray>] values: IBreakpointedProp<int> [])" "(createObj !!values)"
            ]

        | _ -> None

    let regularOverloads =
        match componentMethodName, propMethodName, propDocType with
        | _, _, "unsupportedProp" -> [ RegularPropOverload.createCustom "" "UnsupportedProp ()" ]

        | _, _, "any" when
            markdownDocLines
            |> List.exists (fun s -> s.Contains "The DOM API casts this to a string")
            ->
            [ RegularPropOverload.create "(value: string)" "value" ]

        | AutoComplelete overloads
        | DateAndTimePickers overloads
        | Popover overloads
        | Slider overloads
        | GridAndStack overloads
        | LocalizationProviderRegularOverloads overloads -> overloads

        | "accordion", "onChange", "func" ->
            [ RegularPropOverload.create "(handler: Event -> bool -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: bool -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "alert", "iconMapping", "{ error?: node, info?: node, success?: node, warning?: node }" ->
            [ [ "error", "ReactElement", true
                "info", "ReactElement", true
                "success", "ReactElement", true
                "warning", "ReactElement", true ]
              |> paramListAndObjCreator
              ||> RegularPropOverload.create ]

        | ("bottomNavigation"
          | "tabs"),
          "onChange",
          "func" ->
            [ RegularPropOverload.create "(handler: Event -> 'a -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: 'a -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "buttonBase", "action", "ref" ->
            [ RegularPropOverload.create "(ref: IRefValue<ButtonBaseActions option>)" "ref"
              RegularPropOverload.create "(handler: ButtonBaseActions -> unit)" "handler" ]

        | "buttonBase", "buttonRef", "ref" ->
            [ RegularPropOverload.create "(ref: IRefValue<#Element option>)" "ref"
              RegularPropOverload.create "(handler: #Element -> unit)" "handler" ]

        | "buttonBase", "touchRippleRef", _ -> [ RegularPropOverload.create "(ref: IRefValue<#Element option>)" "ref" ]

        | "buttonUnstyled", "action", "func | { current?: { focusVisible: func } }" ->
            [ RegularPropOverload.create "(ref: IRefValue<ButtonUnstyledActions option>)" "ref"
              RegularPropOverload.create "(handler: ButtonUnstyledActions -> unit)" "handler" ]

        | ("checkbox"
          | "formControlLabel"
          | "switch"),
          "onChange",
          "func" ->
            [ RegularPropOverload.create "(handler: Event -> unit)" "handler"
              RegularPropOverload.create "(handler: bool -> unit)" "(fun (e: Event) -> handler e.Checked)" ]

        | (AllDateTimePickers _
          | AllDatePickers _
          | AllDateRangePickers _
          | AllTimePickers _
          | "checkbox"
          | "filledInput"
          | "formControlLabel"
          | "input"
          | "inputBase"
          | "outlinedInput"
          | "radio"
          | "switch"
          | "textField"),
          "inputRef",
          ("ref"
          | "func | { current?: object }") ->
            [ RegularPropOverload.create "(ref: IRefValue<#Element option>)" "ref"
              RegularPropOverload.create "(handler: #Element -> unit)" "handler" ]

        | ("collapse"
          | "fade"
          | "grow"
          | "slide"
          | "zoom"),
          "easing",
          "{ enter?: string, exit?: string } | string" ->
            [ RegularPropOverload.create "(enter: string, exit: string)" "(enter, exit)"
              RegularPropOverload.create "(both: string)" "both" ]

        | ("collapse"
          | "fade"
          | "grow"
          | "slide"
          | "zoom"),
          "addEndListener",
          "func" ->
            [ RegularPropOverload.create "(listener: unit -> unit)" "listener"
              RegularPropOverload.create "(listenerWithCallback: (unit -> unit) -> unit)" "listenerWithCallback" ]

        | "collapse",
          ("collapsedHeight"
          | "collapsedSize"),
          "number | string" ->
            [ RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: Styles.ICssUnit)" "value" ]

        | "circularProgress", "size", "number | string"
        | "skeleton",
          ("height"
          | "width"),
          "number | string" ->
            [ RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: Styles.ICssUnit)" "value" ]

        | "dialog", "onClose", "func" ->
            [ RegularPropOverload.create "(handler: Event -> DialogCloseReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: DialogCloseReason -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | ("dialog"
          | "menu"
          | "popover"
          | "snackbar"),
          ("onEnter"
          | "onEntered"
          | "onEntering"),
          "func" -> [ RegularPropOverload.create "(handler: ReactElement -> bool -> unit)" "handler" ]

        | ("dialog"
          | "menu"
          | "popover"
          | "snackbar"),
          ("onExit"
          | "onExited"
          | "onExiting"),
          "func" -> [ RegularPropOverload.create "(handler: ReactElement -> unit)" "handler" ]

        | "expansionPanel", "onChange", "func" ->
            [ RegularPropOverload.create "(handler: Event -> bool -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: bool -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | ("formControl"
          | "tableRow"),
          "children",
          "node" ->
            [ RegularPropOverload.createCustom "(element: ReactElement)" "prop.children element"
              RegularPropOverload.createCustom "(elements: seq<ReactElement>)" "prop.children elements" ]

        | "hidden", "only", "'xs' | 'sm' | 'md' | 'lg' | 'xl' | Array<'xs' | 'sm' | 'md' | 'lg' | 'xl'>" ->
            [ RegularPropOverload.create "([<ParamArray>] values: BreakpointKey [])" "values" ]

        | "inputBase", "onBlur", "func" -> [ RegularPropOverload.create "(handler: Event option -> unit)" "handler" ]

        | ("input"
          | "filledInput"
          | "outlinedInput"
          | "inputBase"
          | "textareaAutosize"
          | "textField"),
          ("rows"
          | "rowsMax"
          | "rowsMin"
          | "maxRows"
          | "minRows"),
          "number | string" -> [ RegularPropOverload.create "(value: int)" "value" ]

        | ("input"
          | "filledInput"
          | "outlinedInput"
          | "inputBase"
          | "textField"
          | "nativeSelect"
          | "radioGroup"),
          "onChange",
          "func" ->
            [ RegularPropOverload.create "(handler: Event -> unit)" "handler"
              RegularPropOverload.create "(handler: string -> unit)" "(fun (e: Event) -> handler e.Value)" ]

        | ("filledInput"
          | "input"
          | "inputBase"
          | "outlinedInput"),
          ("startAdornment"
          | "endAdornment"),
          "node" -> [ RegularPropOverload.create "(element: ReactElement)" "element" ]

        | ("filledInput"
          | "input"
          | "inputBase"),
          "inputComponent",
          "elementType"
        | _,
          "component'",
          ("elementType"
          | "element type") ->
            [ RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "(value: ReactElementType)" "value" ]

        | "masonry", "spacing", "Array<number | string> | number | object | string" ->
            [ RegularPropOverload.create "(factor: float)" "factor"
              RegularPropOverload.create "(factor: int)" "factor"
              //RegularPropOverload.create "([<ParamArray>] factors: IBreakpointedProp<int> [])" "(createObj !!factors)"
              //RegularPropOverload.create "([<ParamArray>] factors: IBreakpointedProp<float> [])" "(createObj !!factors)"
              RegularPropOverload.createWithMuiBreakpoints "int"
              RegularPropOverload.createWithMuiBreakpoints "float"
            ]

        | "masonry", "columns", "Array<number | string> | number | object | string" ->
            [ RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.createWithMuiBreakpoints "int" ]
              //RegularPropOverload.create "([<ParamArray>] values: IBreakpointedProp<int> [])" "(createObj !!values)" ]

        | "menu", "onClose", "func" ->
            [ RegularPropOverload.create "(handler: Event -> MenuCloseReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: MenuCloseReason -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "modal", "onClose", "func" ->
            [ RegularPropOverload.create "(handler: Event -> ModalCloseReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: ModalCloseReason -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | ("modal"
          | "portal"),
          "onRendered",
          "func" -> [] // deprecated

        | ("modal"
          | "popover"
          | "popper"
          | "portal"
          | "slide"),
          "container",
          "HTML element | func" ->
            [ RegularPropOverload.create "(element: #Element option)" "element"
              RegularPropOverload.create "(element: ReactElement option)" "element"
              RegularPropOverload.create "(getElement: unit -> #Element option)" "getElement"
              RegularPropOverload.create "(getElement: unit -> ReactElement option)" "getElement" ]

        | "pagination", "getItemAriaLabel", "func" ->
            [ RegularPropOverload.create
                  "(getLabel: string -> int -> bool -> string)"
                  "(Func<_,_,_,_> (fun t p s -> getLabel t p s))" ]

        | "pagination", "onChange", "func" ->
            [ RegularPropOverload.create "(handler: Event -> int -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: int -> unit)" "(Func<_,_> (fun _ p -> handler p))" ]

        | "pagination", "renderItem", "func" ->
            [ RegularPropOverload.create
                  "(render: PaginationRenderItemParams -> ReactElement)"
                  "(Func<_,_> (fun p -> render p))" ]

        | "popper", "anchorEl", "HTML element | object | func" ->
            [ RegularPropOverload.create "(value: #Element option)" "value"
              RegularPropOverload.create "(getElement: unit -> #Element option)" "getElement"
              RegularPropOverload.create "(referenceObject: obj)" "referenceObject"
              RegularPropOverload.create "(getReferenceObject: unit -> obj)" "getReferenceObject"
              RegularPropOverload.create "(ref: IRefValue<#Element option>)" "(fun () -> ref.current)" ]

        | "popper",
          ("modifiers"
          | "popperOptions"),
          _ -> [ RegularPropOverload.create "(value: 'a)" "value" ]

        | "popper", "popperRef", "ref"
        | "rootRef", "rootRef", "refType.isRequired" ->
            [ RegularPropOverload.create "(ref: IRefValue<#Element option>)" "ref"
              RegularPropOverload.create "(handler: #Element -> unit)" "handler" ]

        | "radioGroup", "defaultValue", "any" -> [ RegularPropOverload.create "(value: 'a)" "value" ]

        | "rating", "getLabelText", "func" ->
            [ RegularPropOverload.create "(getText: int -> string)" "getText"
              RegularPropOverload.create "(getText: float -> string)" "getText" ]

        | "rating",
          ("onChange"
          | "onChangeActive"),
          "func" ->
            [ RegularPropOverload.create "(handler: Event -> int -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: Event -> float -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: int -> unit)" "(Func<_,_,_> (fun _ v -> handler v))"
              RegularPropOverload.create "(handler: float -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "rating",
          ("defaultValue"
          | "max"
          | "precision"
          | "value"),
          "number" ->
            [ RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: float)" "value" ]

        | "select", "onChange", "func" ->
            [ RegularPropOverload.create "(handler: Event -> ReactElement -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: 'a -> unit)" "(Func<_,_,_> (fun (e: Event) _ -> handler !!e.Value))" ]

        | "select", "renderValue", "func" ->
            [ RegularPropOverload.create "(render: 'a -> ReactElement)" "render"
              RegularPropOverload.create "(render: 'a -> seq<ReactElement>)" "render"
              RegularPropOverload.create "(render: 'a -> string)" "render"
              RegularPropOverload.create "(render: 'a -> string seq)" "render"
              RegularPropOverload.create "(render: 'a -> int)" "render"
              RegularPropOverload.create "(render: 'a -> float)" "render" ]

        | "select", "value", "'' | any" ->
            [ RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "(value: 'Value option)" "value" ]

        | "snackbar", "onClose", "func" ->
            [ RegularPropOverload.create "(handler: Event -> SnackbarCloseReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: SnackbarCloseReason -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "snackbar", "autoHideDuration", "number" ->
            [ RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: int option)" "value" ]

        | "speedDial", "onClose", "func" ->
            [ RegularPropOverload.create "(handler: Event -> SpeedDialCloseReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create
                  "(handler: SpeedDialCloseReason -> unit)"
                  "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "speedDial", "onOpen", "func" ->
            [ RegularPropOverload.create "(handler: Event -> SpeedDialOpenReason -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: SpeedDialOpenReason -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "stepper", "children", "node" ->
            [ RegularPropOverload.createCustom "(elements: seq<ReactElement>)" "prop.children elements" ]

        | "swipeableDrawer", "hysteresis", "number" -> [ RegularPropOverload.create "(value: float)" "value" ]

        | _, "sx", "Array<func | object | bool> | func | object" ->
            let themeOverrideCallbackValueCode =
                "(Func<Theme, _> (fun theme -> let styleOverrides = themeOverride theme in (createObj !!styleOverrides)))"
            [
                RegularPropOverload.create "(styleOverrides: #seq<IStyleAttribute>)" "(createObj !!styleOverrides)"

                //RegularPropOverload.create "(enabled: bool list)" "enabled"

                RegularPropOverload.createWithMuiBreakpointsAndParamValueTransform
                    "(#seq<IStyleAttribute>)"
                    "!!createObj"

                RegularPropOverload.create
                    "(themeOverride: Theme -> #seq<IStyleAttribute>)"
                    themeOverrideCallbackValueCode

                RegularPropOverload.create
                    "(themeOverrides: (Theme -> #seq<IStyleAttribute>) [])"
                    (sprintf "(themeOverrides |> Array.map (fun themeOverride -> %s))" themeOverrideCallbackValueCode)

                RegularPropOverload.create
                    "(breakpointThemeOverrides: (string * (Theme -> #seq<IStyleAttribute>)) [])"
                    (sprintf "(breakpointThemeOverrides |> Array.map (fun (breakpoint, themeOverride) -> breakpoint, %s) |> !!createObj)" themeOverrideCallbackValueCode)

                RegularPropOverload.createWithMuiBreakpointsAndParamValueTransform
                    "(Theme -> #seq<IStyleAttribute>)"
                    (sprintf "(fun themeOverride -> %s)" themeOverrideCallbackValueCode)

                RegularPropOverload.create
                    "(themeBreakpointOverrides: (Theme -> (string * #seq<IStyleAttribute>) list) [])"
                    "(themeBreakpointOverrides |> Array.map (fun themeBpOverride -> Func<Theme, _> (fun theme -> let bpStyles = themeBpOverride theme in (bpStyles |> List.map (fun (bp, styles) -> bp, createObj !!styles) |> !!createObj))))"
            ]

        | "tabs", "action", "ref" ->
            [ RegularPropOverload.create "(ref: IRefValue<TabsActions option>)" "ref"
              RegularPropOverload.create "(handler: TabsActions -> unit)" "handler" ]

        | "tablePagination", "rowsPerPageOptions", _ ->
            [ RegularPropOverload.create "([<ParamArray>] values: int [])" "(ResizeArray values)"
              RegularPropOverload.create
                  "([<ParamArray>] values: {| label: string; value: int |} [])"
                  "(ResizeArray values)" ]

        | "tablePagination", "labelDisplayedRows", "func" ->
            [ RegularPropOverload.create
                  "(getLabel: {| from: int; ``to``: int; count: int |} -> ReactElement)"
                  "getLabel" ]

        | "tablePagination", "onChangePage", "func" ->
            [ RegularPropOverload.create "(handler: Event -> int -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: int -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "toggleButtonGroup", "value", "any" ->
            [ RegularPropOverload.create "(value: 'toggleButtonValue option)" "value"
              RegularPropOverload.create "(values: 'toggleButtonValue [])" "(ResizeArray values)" ]

        | "toggleButtonGroup", "onChange", "func" ->
            [ RegularPropOverload.create "(handler: Event -> 'a -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: 'a -> unit)" "(Func<_,_,_> (fun _ v -> handler v))" ]

        | "treeView",
          ("expanded"
          | "defaultExpanded"),
          "Array<string>" -> [ RegularPropOverload.create "([<ParamArray>] nodeIds: string [])" "nodeIds" ]

        | "treeView", "onNodeToggle", "func" ->
            [ RegularPropOverload.create "(handler: Event -> string [] -> unit)" "handler" ]

        | "treeView",
          ("defaultSelected"
          | "selected"),
          "Array<string> | string" ->
            [ RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "([<ParamArray>] values: string [])" "values" ]

        | "treeView", "onNodeSelect", "func" ->
            [ RegularPropOverload.create "(handler: Event -> string -> unit)" "(Func<_,_,_> handler)"
              RegularPropOverload.create "(handler: Event -> string [] -> unit)" "(Func<_,_,_> handler)" ]

        | "typography", "variantMapping", "object" ->
            [ [ "h1", "string", true
                "h2", "string", true
                "h3", "string", true
                "h4", "string", true
                "h5", "string", true
                "h6", "string", true
                "subtitle1", "string", true
                "subtitle2", "string", true
                "body1", "string", true
                "body2", "string", true ]
              |> paramListAndObjCreator
              ||> RegularPropOverload.create ]

        | "unstable_TrapFocus", "getDoc", "func" -> [ RegularPropOverload.create "(getDoc: unit -> Document)" "getDoc" ]

        | "unstable_TrapFocus", "isEnabled", "func" ->
            [ RegularPropOverload.create "(isEnabled: unit -> bool)" "isEnabled" ]

        | _, "getItemAriaLabel", "func" ->
            [ RegularPropOverload.create "(getAriaLabel: string -> string)" "(Func<_, _> getAriaLabel)" ]

        | _, "transitionDuration", "number | { enter?: number, exit?: number }" ->
            [ RegularPropOverload.create "(value: int)" "value"
              [ "enter", "int", true
                "exit", "int", true ]
              |> paramListAndObjCreator
              ||> RegularPropOverload.create ]

        | _, "transitionDuration", "number | { appear?: number, enter?: number, exit?: number }" ->
            [ RegularPropOverload.create "(value: int)" "value"
              [ "appear", "int", true
                "enter", "int", true
                "exit", "int", true ]
              |> paramListAndObjCreator
              ||> RegularPropOverload.create ]

        | _,
          ("transitionDuration"
          | "timeout"),
          ("'auto' | number | { appear?: number, enter?: number, exit?: number }"
          | "number | { appear?: number, enter?: number, exit?: number }") ->
            [ RegularPropOverload.create "(value: int)" "value"
              [ "appear", "int", true
                "enter", "int", true
                "exit", "int", true ]
              |> paramListAndObjCreator
              ||> RegularPropOverload.create ]

        | _, "anchorEl", "HTML element | func" ->
            [ RegularPropOverload.create "(value: #Element option)" "value"
              RegularPropOverload.create "(getElement: unit -> #Element option)" "getElement"
              RegularPropOverload.create "(ref: IRefValue<#Element option>)" "(fun () -> ref.current)" ]

        | _,
          pn,
          ("elementType"
          | "element type") when pn.EndsWith "Component" ->
            [ RegularPropOverload.create "(value: ReactElementType)" "value" ]

        | _,
          pn,
          ("object"
          | "{ component?: element type }") when pn.EndsWith "Props" ->
            [ RegularPropOverload.create "(props: IReactProperty list)" "(createObj !!props)" ]

        | _, pn, "object" when pn = "classes" || pn.EndsWith "Classes" -> []

        | _, _, t when
            t = "bool"
            || t.Split('|')
               |> Array.map String.trim
               |> Array.contains "bool"
            ->
            [ RegularPropOverload.create "(value: bool)" "value" ]

        | _, _, "string" -> [ RegularPropOverload.create "(value: string)" "value" ]

        | "gridList", "cellHeight", "number | 'auto'"
        | _, _, "number" -> [ RegularPropOverload.create "(value: int)" "value" ]

        | _, pn, "func" when pn.StartsWith "on" -> [ RegularPropOverload.create "(handler: Event -> unit)" "handler" ]

        | _, pn, "node" when pn = "icon" || pn.EndsWith "Icon" ->
            [ RegularPropOverload.create "(element: ReactElement)" "element" ]

        | _, "children", "node"
        | "tabList", "children", "Array<element>"
        | "popper", "children", "node | func" -> // TODO: popper.children can also be a func, but can't find signature docs
            [ RegularPropOverload.createCustom "(element: ReactElement)" "prop.children element"
              RegularPropOverload.createCustom "(elements: seq<ReactElement>)" "prop.children elements"
              RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "(values: string seq)" "values"
              RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: float)" "value" ]

        // Fixes the broken docs of imageList
        | "imageList", "cellHeight", "number | oneOf(['auto'" -> [ RegularPropOverload.create "(value: int)" "value" ]

        | component', ("componentsProps" as prop), fields ->
            let translateType (t: string) =
                //if type' = "object" then "IReactProperty list" else type'
                match t.Trim() with
                | "object" -> Some "seq<IReactProperty>"
                | "func" -> None
                | otherType -> Some otherType

            fields.TrimStart('{').TrimEnd('}').Split(',')
            |> Array.map (fun p -> p.Split(':') |> Array.map (fun s -> s.Trim()))
            |> Array.choose (function
                | [| key; type' |] ->
                    let isOptional = key.EndsWith('?')
                    let typeCases = type'.Split('|') |> Array.choose translateType

                    match typeCases with
                    | PropsTypeUnion component' prop translatedType ->
                        Some(key.TrimEnd('?'), translatedType, isOptional)
                    | _ -> None

                | parts ->
                    printfn "Invalid parts in %s: %A" component' parts
                    None)
            |> Array.toList
            |> paramListAndObjCreator
            ||> RegularPropOverload.create
            |> List.singleton

        | component', "components", "object" -> [ RegularPropOverload.create "(value: obj)" "value" ]

        // | ("badge" | "badgeUnstyled"), "components", "{ Badge?: elementType, Root?: elementType }"
        | component', "components", fields ->
            let parsedFields =
                fields.TrimStart('{').TrimEnd('}').Split(',')
                |> Array.map (fun p -> p.Split(':'))
                |> Array.choose (function
                    | [| key; _ |] ->
                        let isOptional = key.EndsWith('?')
                        Some(key.TrimEnd('?'), isOptional)
                    | parts ->
                        printfn "Invalid parts in %s: %A" component' parts
                        None)
                |> Array.toList

            let strings =
                parsedFields
                |> List.map (fun (name, isOptional) -> (name, "string", isOptional))
                |> paramListAndObjCreator
                ||> RegularPropOverload.create

            let reactElementTypes =
                parsedFields
                |> List.map (fun (name, isOptional) -> (name, "ReactElementType", isOptional))
                |> paramListAndObjCreator
                ||> RegularPropOverload.create

            [ strings; reactElementTypes ]

        | _, _, "node" ->
            [ RegularPropOverload.create "(value: ReactElement)" "value"
              RegularPropOverload.create "(values: seq<ReactElement>)" "values"
              RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "(values: string seq)" "values"
              RegularPropOverload.create "(value: int)" "value"
              RegularPropOverload.create "(value: float)" "value" ]

        | _, _, "element" -> [ RegularPropOverload.create "(value: ReactElement)" "value" ]

        | _, _, "element | string" ->
            [ RegularPropOverload.create "(value: string)" "value"
              RegularPropOverload.create "(value: ReactElement)" "value" ]

        | _, _, "any" -> [ RegularPropOverload.create "(value: 'a)" "value" ]

        | _, name, "integer" -> [ RegularPropOverload.create (sprintf "(%s: int)" name) name ]

        | _, name, "bool" -> [ RegularPropOverload.create (sprintf "(%s: bool)" name) name ]

        | _, name, "func" when name.StartsWith "render" ->
            [ RegularPropOverload.create "(renderer: unit -> ReactElement)" "renderer" ]

        | _ when isProbablyEnumProp -> []

        | _ -> [ RegularPropOverload.create "(value: TODO)" "value" ]

    let enumOverloads =
        match componentMethodName, propMethodName, propDocType with
        | "localizationProvider", "dateAdapter", "func" ->
            [ EnumPropOverload.create "dayJs" @"(import ""AdapterDayjs"" ""@mui/x-date-pickers/AdapterDayjs"")"
              EnumPropOverload.create "dateFns" @"(import ""AdapterDateFns"" ""@mui/x-date-pickers/AdapterDateFns"")"
              EnumPropOverload.create "luxon" @"(import ""AdapterLuxon"" ""@mui/x-date-pickers/AdapterLuxon"")"
              EnumPropOverload.create "moment" @"(import ""AdapterMoment"" ""@mui/x-date-pickers/AdapterMoment"")" ]

        | _ when not isProbablyEnumProp -> []

        | ("grid" | "stack"), "direction", _ ->
            let cases =
                [| "column-reverse"
                   "column"
                   "row-reverse"
                   "row" |]
                |> Array.map (fun c -> c, kebabCaseToCamelCase c)

            [ for (jsCaseId, translatedCaseId) in cases do
                  EnumPropOverload.create translatedCaseId (sprintf "%A" jsCaseId) ]

        | "hidden", "only", "'xs' | 'sm' | 'md' | 'lg' | 'xl' | Array<'xs' | 'sm' | 'md' | 'lg' | 'xl'>" ->
            [ EnumPropOverload.create "xs" "\"xs\""
              EnumPropOverload.create "sm" "\"sm\""
              EnumPropOverload.create "md" "\"md\""
              EnumPropOverload.create "lg" "\"lg\""
              EnumPropOverload.create "xl" "\"xl\"" ]

        | "snackbar", "anchorOrigin", "{ horizontal: 'center' | 'left' | 'right', vertical: 'bottom' | 'top' }" ->
            [ EnumPropOverload.create "topLeft" "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "topCenter"
                  "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"center\" ])"
              EnumPropOverload.create
                  "topRight"
                  "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"right\" ])"
              EnumPropOverload.create
                  "bottomLeft"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "bottomCenter"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"center\" ])"
              EnumPropOverload.create
                  "bottomRight"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"right\" ])" ]

        | "popover",
          ("anchorOrigin"
          | "transformOrigin"),
          "{ horizontal: 'center' | 'left' | 'right' | number, vertical: 'bottom' | 'center' | 'top' | number }" ->
            [ EnumPropOverload.create "topLeft" "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "topCenter"
                  "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"center\" ])"
              EnumPropOverload.create
                  "topRight"
                  "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"right\" ])"
              EnumPropOverload.create
                  "centerLeft"
                  "(createObj [ \"vertical\" ==> \"center\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "centerCenter"
                  "(createObj [ \"vertical\" ==> \"center\"; \"horizontal\" ==> \"center\" ])"
              EnumPropOverload.create
                  "centerRight"
                  "(createObj [ \"vertical\" ==> \"center\"; \"horizontal\" ==> \"right\" ])"
              EnumPropOverload.create
                  "bottomLeft"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "bottomCenter"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"center\" ])"
              EnumPropOverload.create
                  "bottomRight"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"right\" ])" ]

        | ("badge"
          | "badgeUnstyled"),
          "anchorOrigin",
          "{ horizontal: 'left' | 'right', vertical: 'bottom' | 'top' }" ->
            [ EnumPropOverload.create "topLeft" "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "topRight"
                  "(createObj [ \"vertical\" ==> \"top\"; \"horizontal\" ==> \"right\" ])"
              EnumPropOverload.create
                  "bottomLeft"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"left\" ])"
              EnumPropOverload.create
                  "bottomRight"
                  "(createObj [ \"vertical\" ==> \"bottom\"; \"horizontal\" ==> \"right\" ])" ]
        | "imageList", "cellHeight", "number | oneOf(['auto'"
        | ("dateTimePicker"
          | "datePicker"
          | "calendarPicker"
          | "clockPicker"),
          "views",
          _ -> []

        | _ ->
            let enumValueExpressions =
                propDocType.Split("|")
                |> Array.toList
                |> List.choose (fun s ->
                    let value = s.Trim()

                    if value.StartsWith "'" && value.EndsWith "'" then
                        // String
                        value.Replace("'", "\"") |> Some
                    elif value = "number"
                         || value = "bool"
                         || value.Contains "{"
                         || String.IsNullOrWhiteSpace value then
                        None
                    else
                        // Probably literal, e.g. bool or int
                        Some value)

            let overloads =
                enumValueExpressions
                |> List.map (fun v ->
                    let methodName =
                        v.Trim('"')
                        |> kebabCaseToCamelCase
                        |> prefixUnderscoreToNumbers
                        |> appendApostropheToReservedKeywords

                    EnumPropOverload.create methodName v)

            if overloads.IsEmpty then
                failwithf "No enum overloads for %s.%s" componentMethodName propMethodName

            overloads

    let globalDocTransform (s: string) =
        s
            .Replace(" It supports those theme colors that make sense for this component.", "")
            .Replace(" You may specify a single timeout for all transitions, or individually with an object.", "")
            .Replace("event.target.value", "event.Value")
            .Replace(" The DOM API casts this to a string.", "")

    let propSpecificDocTransform (s: string) =
        match componentMethodName, propMethodName with
        | "nativeSelect", "children" -> s.Replace("Can be some `` elements.", "Can be some `<option>` elements.")
        | "stepper", "children" -> s.Replace("Two or more `` components.", "Two or more `<Step />` components.")
        | "tableRow", "children" ->
            s
                .Replace("Should be valid | children", "Should be valid `<tr>` children")
                .Replace("|  |", "")
        | "formHelperText", "children" -> s.Replace("If `''` is provided", "If `' '` is provided")

        | _ -> s

    let transformedMarkdownDocLines =
        markdownDocLines
        |> List.map globalDocTransform
        |> List.map propSpecificDocTransform
        |> List.trimEmptyLines

    Prop.create realPropName propMethodName
    |> Prop.setDocs transformedMarkdownDocLines
    |> Prop.setRequired isRequired
    |> Prop.addRegularOverloads regularOverloads
    |> Prop.addEnumOverloads enumOverloads

let parseComponent (htmlPathOrUrl: string) =

    Console.WriteLine(sprintf "Processing component %s" htmlPathOrUrl)

    let page = ComponentApiPage.Load(htmlPathOrUrl)
    let html = page.Html

    let importDefaultMatches =
        Regex.Match(html.CssSelect("pre").[0].InnerText(), "import ?(.+?) ?from ?'(.+?)'")

    if importDefaultMatches.Length = 0 then
        failwith "No import matches"

    let compMethodName =
        importDefaultMatches.Groups.[1].Value
        |> String.lowerFirst

    let importPath = importDefaultMatches.Groups.[2].Value

    let compMethodName, importSelector =
        let matches = Regex.Match(compMethodName, "{ ?(.+?)? ?}")

        if matches.Length = 0 then
            compMethodName, None
        else
            matches.Groups.[1].Value |> String.lowerFirst, Some matches.Groups.[1].Value

    let noteNodes1 =
        html.CssSelect(".markdown-body").[0].Elements()
        |> List.skipWhile (fun n -> n.Name() <> "table")
        |> List.trySkip 1
        |> List.takeWhile (fun n -> n.Name() = "p")

    let noteNodes2 =
        html.CssSelect(".markdown-body").[0].Elements()
        |> List.skipWhile (fun n -> n.Name() <> "h2" || n.InnerText() <> "Notes")
        |> List.trySkip 1
        |> List.takeWhile (fun n -> n.Name() = "p")

    let markdownDocLines =
        noteNodes1 @ noteNodes2
        |> docElementsToMarkdownLines
        |> fun ls ->
            if importPath.StartsWith "@mui/lab" then
                "**This is an experimental component from @mui/lab. Breaking changes may occur at any time.**"
                :: "" :: ls
            else
                ls

    let additionalOverloads =
        match compMethodName with
        | "dialogContentText"
        | "dialogTitle"
        | "formHelperText"
        | "formLabel"
        | "inputLabel"
        | "listSubheader"
        | "stepLabel"
        | "tableCell"
        | "typography" -> [ ComponentOverload.create "(text: string)" "[ prop.children (Html.text text) ]" ]
        | "icon" -> [ ComponentOverload.create "(name: string)" "[ prop.children (Html.text name) ]" ]
        | "listItemText" -> [ ComponentOverload.create "(primary: string)" "[ listItemText.primary primary ]" ]
        | _ -> []

    let props =
        page.Tables.Props.Rows
        |> Array.mapi (fun i r ->
            let rowHtml = page.Tables.Props.Html.CssSelect("tbody > tr").[i]
            parseProp compMethodName r rowHtml)

    let props =
        // TODO: Remove when docs are fixed:
        // https://github.com/mui-org/material-ui/issues/21711#issuecomment-657233933
        match compMethodName with
        | "box"
        | "container"
        | "localizationProvider" ->
            let children =
                Prop.create "children" "children"
                |> Prop.addRegularOverloads [
                    RegularPropOverload.createCustom
                        "(element: ReactElement)"
                        "prop.children element"
                    RegularPropOverload.childrenElementsSeq()
                    RegularPropOverload.create "(value: string)" "value"
                    RegularPropOverload.create "(values: string seq)" "values"
                    RegularPropOverload.create "(value: int)" "value"
                    RegularPropOverload.create "(value: float)" "value"
                ]

            Array.append [| children |] props

        | _ -> props

    let addChildrenOverloadIfSupported (comp: Component) =
        let hasReactElementSeqChildren =
            comp.Props
            |> List.exists (fun p ->
                p.MethodName = "children"
                && p.RegularOverloads
                   |> List.exists (fun o -> o.ParamsCode = "(elements: seq<ReactElement>)"))

        if not hasReactElementSeqChildren then
            comp
        else
            // Use #seq<ReactElement> to help overload resolution when using empty lists
            let overload =
                ComponentOverload.create
                    "(children: #seq<ReactElement>)"
                    (sprintf "[ Feliz.MaterialUI.%s.children (children :> seq<ReactElement>) ]" comp.MethodName)

            comp |> Component.addOverload overload

    let addUnsupportedChildrenProp (comp: Component) =
        let hasChildren =
            comp.Props
            |> List.exists (fun p -> p.MethodName = "children")

        if hasChildren then
            comp
        else
            let prop =
                Prop.create "children" "children"
                |> Prop.setDocs [ "This component does not support children." ]
                |> Prop.addRegularOverload (RegularPropOverload.createCustom "" "UnsupportedProp ()")

            comp |> Component.addProp prop

    let componentName =
        Regex
            .Match(
                page.Html.Body().ToString(),
                "The \<code\>(.+?)\<\/code\> name can be used for"
            )
            .Groups.[1]
            .Value
        |> Some
        |> Option.filter (not << String.IsNullOrEmpty)
        |> Option.orElse (Some("Mui" + String.upperFirst compMethodName)) // TODO: Remove when resolved: https://github.com/mui-org/material-ui/issues/20556

    let setInheritance =
        let inheritFrom =
            Regex
                .Match(
                    page.Html.Body().InnerText(),
                    @"While not explicitly documented above, the props of the\s(.+?)\scomponent are also available on (.+?)\."
                )
                .Groups[1]
                .Value
        //Regex.Match(page.Html.Body().InnerText(), "Any other props supplied will be provided to the root element \((.+?)\)")
        //  .Groups.[1].Value
        match compMethodName, inheritFrom with
        | _,
          (null
          | "") -> id
        | ("collapse"
          | "fade"
          | "grow"
          | "slide"
          | "zoom"),
          "Transition" -> id // The Transition component is from an external library
        | _,
          ("native component"
          | "native element") -> id // Native DOM inheritance not currently supported

        | _, baseComp when baseComp.EndsWith("Unstyled") -> id // *Unstyled components from @mui/base are not yet supported.

        | _, baseComp ->
            baseComp
            |> String.lowerFirst
            |> Component.inheritsPropsFrom

    { GeneratorComponent =
        match importSelector with
        | None -> Component.createImportDefault compMethodName importPath
        | Some selector -> Component.createImportSelector compMethodName selector importPath
        |> Component.setDocs markdownDocLines
        |> Component.addOverloads additionalOverloads
        |> Component.addProps props
        |> addChildrenOverloadIfSupported
        |> addUnsupportedChildrenProp
        |> setInheritance
      ClassRules =
        let rowsAndHtml =
            try
                page.Tables.CSS.Rows
                |> Array.mapi (fun i r -> r, page.Tables.CSS.Html.CssSelect("tbody > tr").[i])
                |> Array.toList
            with
            | :? KeyNotFoundException -> []

        rowsAndHtml
        |> List.choose (fun (r, html) -> parseClassRule r html)
      ComponentName = componentName }

let parseApi () =

    let components =
        HtmlCache.getApiFiles ()
        // TODO: Support DataGrid and XGrid
        |> Array.filter (fun path ->
            not <| path.EndsWith "data-grid.html"
            && not <| path.EndsWith "x-grid.html")
        |> Array.Parallel.map parseComponent
        |> Array.toList

    let themeProvider =
        Component.createImportSelector "themeProvider" "ThemeProvider" "@mui/material/styles"
        |> Component.addChildrenProp
        |> Component.addProp (
            Prop.create "theme" "theme"
            |> Prop.setDocs [ "A theme object. You can provide a function to extend the outer theme." ]
            |> Prop.addRegularOverload (RegularPropOverload.create "(theme: Theme)" "theme")
            |> Prop.addRegularOverload (RegularPropOverload.create "(extendTheme: Theme -> Theme)" "extendTheme")
        )

    // https://material-ui.com/styles/api/#stylesprovider
    let stylesProvider =
        Component.createImportSelector "stylesProvider" "StylesProvider" "@mui/styles"
        |> Component.setDocs [
            "This component allows you to change the behavior of the styling solution. It makes the options available down the React tree thanks to the context."
            ""
            "It should preferably be used at **the root of your component tree**."
        ]
        |> Component.addChildrenProp
        |> Component.addProp (
            Prop.create "disableGeneration" "disableGeneration"
            |> Prop.setDocs [ "You can disable the generation of the styles with this option. It can be useful when traversing the React tree outside of the HTML rendering step on the server. Let's say you are using react-apollo to extract all the queries made by the interface server-side. You can significantly speed up the traversal with this property." ]
            |> Prop.addRegularOverload (RegularPropOverload.create "(value: bool)" "value")
        )
        |> Component.addProp (
            Prop.create "injectFirst" "injectFirst"
            |> Prop.setDocs [ "By default, the styles are injected last in the `<head>` element of the page. As a result, they gain more specificity than any other style sheet. If you want to override Material-UI's styles, set this prop." ]
            |> Prop.addRegularOverload (RegularPropOverload.create "(value: bool)" "value")
        )
        |> Component.addProp (
            Prop.create "jss" "jss"
            |> Prop.setDocs [ "JSS's instance." ]
            |> Prop.addRegularOverload (RegularPropOverload.create "(value: obj)" "value")
        )

    let styledEngineProvider =
        Component.createImportSelector "styledEngineProvider" "StyledEngineProvider" "@mui/material/styles"
        |> Component.setDocs [
            "This component allows you to change styes import precedence and thereby override default MUI styles."
            "Documentation: https://mui.com/material-ui/guides/interoperability/#css-injection-order"
        ]
        |> Component.addProp (
            Prop.create "injectFirst" "injectFirst"
            |> Prop.setDocs ["By default, the styles are injected last in the `<head>` element of the page. As a result, they gain more specificity than any other style sheet. If you want to override MUI's styles, set this prop."]
            |> Prop.addRegularOverload (RegularPropOverload.create "(value: bool)" "value")
        )
        |> Component.addChildrenProp

    let cacheProvider =
        Component.createImportSelector "cacheProvider" "CacheProvider" "@emotion/reac"
        |> Component.setDocs [
            "If you are using Emotion and have a custom cache in your app, it will override the one coming from MUI."
            "In order for the injection order to still be correct, you need to add the prepend option."
        ]
        |> Component.addProp (
            Prop.create "prepend" "prepend"
            |> Prop.setDocs [
                "`true` moves MUI styles to the top of the `<head>` so they're loaded first."
                "It allows developers to easily override MUI styles with other styling solutions, like CSS modules."
            ]
            |> Prop.addRegularOverload (
                RegularPropOverload.create "(value: bool)" "value"
            )
        )
        |> Component.addProp (
            Prop.create "key" "key"
            |> Prop.addRegularOverload (
                RegularPropOverload.create "(value: string)" "value"
            )
        )
        |> Component.addChildrenProp

    let api =
        ComponentApi.create "Feliz.MaterialUI" "Mui"
        |> ComponentApi.addComponent themeProvider
        |> ComponentApi.addComponent stylesProvider
        |> ComponentApi.addComponent styledEngineProvider
        |> ComponentApi.addComponent cacheProvider
        |> ComponentApi.addComponents (
            components
            |> List.map (fun c -> c.GeneratorComponent)
        )
        |> ComponentApi.setParseProps "!!properties |> Object.fromFlatEntries"

    { GeneratorComponentApi = api
      MuiComponents = components }
