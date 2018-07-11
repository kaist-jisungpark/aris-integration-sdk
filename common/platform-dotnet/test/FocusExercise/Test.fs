﻿module Test

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Serilog
open SoundMetrics.Aris.Comms
open SoundMetrics.Scripting
open System
open System.Threading
open System.Windows.Threading

type FU = int
type AvailableSonars = Beacons.BeaconSource<SonarBeacon, SerialNumber>


//-----------------------------------------------------------------------------
// Synchronization Context
//
// In a console application (like this) the sync context can be problematic.
// GUI apps are much easier this way.
// This code uses the Dispatcher.PushFrame technique available in the .NET
// Framework (Desktop).
//-----------------------------------------------------------------------------

let getExplorerBeacon (availables : AvailableSonars) targetSN : SonarBeacon option =

    let timeout = TimeSpan.FromSeconds(4.0)

    let beacon =
        let mutable someBeacon = None
        let frame = DispatcherFrame()
        Async.Start (async {
            try
                let! b = waitForExplorerBySerialNumberAsync availables timeout targetSN
                someBeacon <- b
            finally
                frame.Continue <- false
        })
        Dispatcher.PushFrame(frame)
        someBeacon
    beacon


//-----------------------------------------------------------------------------

open SoundMetrics.Scripting.EventMatcher


let runTest eventSource (series : SetupAndMatch<SyslogMessage, unit> array) timeout =

    waitForAsyncWithDispatch
        (runSetupMatchValidateAsync eventSource () series timeout)

let exit code = Environment.Exit(code)

let isFocusRequest = Func<SyslogMessage,bool>(function | ReceivedFocusCommand _ -> true | _ -> false)
let isFocusState = Func<SyslogMessage,bool>(function | UpdatedFocusState _ -> true | _ -> false)

let testRawFocusUnits (eventSource : IObservable<SyslogMessage>) =

    Log.Debug("testRawFocusUnits")

    SynchronizationContext.SetSynchronizationContext(DispatcherSynchronizationContext())
    assert (SynchronizationContext.Current <> null)

    use availables = BeaconListeners.createDefaultSonarBeaconListener SynchronizationContext.Current

    let targetSN = 24
    let beacon = getExplorerBeacon availables targetSN

    let runTest systemType =
        use conduit = new SonarConduit(
                        AcousticSettings.DefaultAcousticSettingsFor(systemType),
                        targetSN,
                        availables,
                        FrameStreamReliabilityPolicy.DropPartialFrames)

        // wait for connection
        waitForAsyncWithDispatch (async { do! Async.Sleep(1000)
                                          return true }) |> ignore

        let series = [|
            {
                SetupAndMatch.Description = "My first step"
                SetUp = fun _ ->    conduit.RequestFocusDistance(2.0<m>)
                                    conduit.RequestFocusDistance(1.0<m>)
                                    true
                Expecteds =
                [|
                    { Description = "Found focus request"; Match = isFocusRequest }
                    { Description = "Observed focus state"; Match = isFocusState }
                |]
            }
            {
                SetupAndMatch.Description = "My second step"
                SetUp = fun _ ->    conduit.RequestFocusDistance(3.0<m>)
                                    true
                Expecteds =
                [|
                    { Description = "Found focus request"; Match = isFocusRequest }
                    { Description = "Observed focus state"; Match = isFocusState }
                |]
            }
        |]

        if not (runTest eventSource series (TimeSpan.FromSeconds(10.0))) then
            Log.Error("Test failed")
            exit 1
        else
            Log.Information("Test succeeded")

    match beacon with
    | Some b -> Log.Information("Found SN {targetSN}", targetSN)
                runTest b.SystemType
    | None -> Log.Error("Couldn't find a beacon for SN {targetSN}", targetSN)

    Log.Information("testRawFocusUnits completed.")
