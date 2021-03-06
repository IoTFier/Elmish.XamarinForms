﻿// Copyright 2018 Elmish.XamarinForms contributors. See LICENSE.md for license.
namespace CounterApp

open System
open System.Diagnostics
open Elmish
open Elmish.XamarinForms
open Elmish.XamarinForms.DynamicViews
open Xamarin.Forms

module App = 
    type Model = 
      { Count : int
        Step : int
        TimerOn: bool }

    type Msg = 
        | Increment 
        | Decrement 
        | Reset
        | SetStep of int
        | TimerToggled of bool
        | TimedTick

    let initModel = { Count = 0; Step = 1; TimerOn=false }

    let init () = initModel, Cmd.none

    let timerCmd = 
        async { do! Async.Sleep 200
                return TimedTick }
        |> Cmd.ofAsyncMsg

    let update msg model =
        match msg with
        | Increment -> { model with Count = model.Count + model.Step }, Cmd.none
        | Decrement -> { model with Count = model.Count - model.Step }, Cmd.none
        | Reset -> init ()
        | SetStep n -> { model with Step = n }, Cmd.none
        | TimerToggled on -> { model with TimerOn = on }, (if on then timerCmd else Cmd.none)
        | TimedTick -> if model.TimerOn then { model with Count = model.Count + model.Step }, timerCmd else model, Cmd.none

    let view (model: Model) dispatch =
        Xaml.ContentPage(
          content=Xaml.StackLayout(padding=20.0,
            children=[ 
              yield 
                Xaml.StackLayout(padding=20.0, verticalOptions=LayoutOptions.Center,
                  children=[
                    Xaml.Label(text= sprintf "%d" model.Count, horizontalOptions=LayoutOptions.Center, fontSize = "Large")
                    Xaml.Button(text="Increment", command= fixf (fun () -> dispatch Increment))
                    Xaml.Button(text="Decrement", command= fixf (fun () -> dispatch Decrement))
                    Xaml.StackLayout(padding=20.0, orientation=StackOrientation.Horizontal, horizontalOptions=LayoutOptions.Center,
                                    children = [ Xaml.Label(text="Timer")
                                                 Xaml.Switch(isToggled=model.TimerOn, toggled=fixf(fun on -> dispatch (TimerToggled on.Value))) ])
                    Xaml.Slider(minimum=0.0, maximum=10.0, value= double model.Step, valueChanged=fixf(fun args -> dispatch (SetStep (int (args.NewValue + 0.5)))))
                    Xaml.Label(text=sprintf "Step size: %d" model.Step, horizontalOptions=LayoutOptions.Center) 
                  ])
              // If you want the button to disappear when in the initial condition then use this:
              //if model <> initModel then 
              yield Xaml.Button(text="Reset", horizontalOptions=LayoutOptions.Center, command=fixf(fun () -> dispatch Reset), canExecute = (model <> initModel))
            ]))

open App

type CounterApp () as app = 
    inherit Application ()

    let program = Program.mkProgram App.init App.update App.view
    let runner = 
        program
        |> Program.withConsoleTrace
        |> Program.withDynamicView app
        |> Program.run
    

#if !NO_SAVE_MODEL_WITH_JSON
    let modelId = "model"
    override __.OnSleep() = 
        let json = MBrace.FsPickler.Json.FsPickler.CreateJsonSerializer().PickleToString(runner.Model)
        Debug.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Debug.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 
                Debug.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = MBrace.FsPickler.Json.FsPickler.CreateJsonSerializer().UnPickleOfString(json)
                Debug.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.Model <- model
            | _ -> ()
        with ex -> 
            program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = this.OnResume()

#endif

#if SAVE_MODEL_BIT_BY_BIT
    let modelId = "model"
    override __.OnSleep() = 
        Debug.WriteLine "OnSleep: saving model into app.Properties"
        app.Properties.["count"] <- runner.Model.Count
        app.Properties.["step"] <- runner.Model.Step
        app.Properties.["timerOn"] <- runner.Model.TimerOn

    override __.OnResume() = 
        Debug.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue("count"),
                  app.Properties.TryGetValue("step"),
                  app.Properties.TryGetValue("timerOn") with
            | (true, (:? int32 as count)), (true, (:? int32 as step)), (true, (:? bool as timerOn)) -> 
                Debug.WriteLine "OnResume: restored model from app.Properties"
                runner.Model <- { Count=count; Step=step; TimerOn=timerOn }
            | _ -> 
                Debug.WriteLine "OnResume: no model found to restore from app.Properties"
                ()
        with ex -> 
            program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = this.OnResume()
#endif