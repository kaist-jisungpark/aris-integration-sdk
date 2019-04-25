﻿// Copyright 2014-2019 Sound Metrics Corp. All Rights Reserved.

namespace SoundMetrics.Aris.AcousticSettings.Experimental

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open SoundMetrics.Aris.AcousticSettings.UnitsOfMeasure
open System

type LegacyFrameRate =
    | Maximum
    | Custom of FrameRate

type LegacyDetail =
    | Auto
    | CustomSamplePeriod of int<Us>

type LegacySampleCount = {
    SampleCount : int
    IsFixed : bool
}

type LegacyTransmit = Off | Minimum | Maximum

type LegacyFrequency = Low | High | Auto

type LegacyPulseWidth =
    | Auto
    | Custom of int<Us>
    | Narrow
    | Medium
    | Wide

/// `LegacyAcousticSettings` serves as the projection for `AcousticSettings`
/// as it was used in past work. While it largely just encloses `AcousticSettings`,
/// it is still a necessary and type-safe part of the new Projection-to-Settings
/// scheme. (This type is closer to the "view model" than the "model.")
type LegacyAcousticSettings = {
    FrameRate : LegacyFrameRate
    SampleCount : LegacySampleCount
    SampleStartDelay : int<Us>
    Detail : LegacyDetail // SamplePeriod
    PulseWidth : LegacyPulseWidth
    PingMode : PingMode
    Transmit : LegacyTransmit
    Frequency : LegacyFrequency
    ReceiverGain : int
}
with
    override s.ToString () = sprintf "%A" s

    static member Invalid = {
        FrameRate = LegacyFrameRate.Custom 0.0</s>
        SampleCount = { SampleCount = 0; IsFixed = true }
        SampleStartDelay = 0<Us>
        Detail = CustomSamplePeriod 0<Us>
        PulseWidth = LegacyPulseWidth.Custom 0<Us>
        PingMode = PingMode.InvalidPingMode 0
        Transmit = Off
        Frequency = Low
        ReceiverGain = 0
    }

type LegacyAcousticSettingsChange =
    | FrameRate of          LegacyFrameRate
    | SampleCount of        LegacySampleCount
    | SampleStartDelay of   int<Us>
    | Detail of             LegacyDetail // SamplePeriod
    | PulseWidth of         LegacyPulseWidth
    | PingMode of           PingMode
    | Transmit of           LegacyTransmit
    | Frequency of          LegacyFrequency
    | ReceiverGain of       int
    | All of                LegacyAcousticSettings

    // Higher-level actions

    | ResetDefaults
    | ChangeDownrangeStart of   float<m>
    | ChangeDownrangeEnd of     float<m>
    /// Translates the window up- or down-range while holding window size in meters constant
    /// (unless fixed sample count, etc., come into play).
    | TranslateWindow of        downrangeStart: float<m>
    | Antialiasing of           int<Us>

module LegacyAcousticSettings =

    [<CompiledName("LegacyAcousticSettingsProjection")>]
    let legacyAcousticSettingsProjection =

        let toSettings (settings: LegacyAcousticSettings) externalContext : AcousticSettings =

            failwith "nyi"

        let change settings systemContext change : LegacyAcousticSettings =

            failwith "nyi"

        let constrain (settings: LegacyAcousticSettings) (systemContext: SystemContext) =

            failwith "nyi"

        {
            new IProjectionMap<LegacyAcousticSettings,LegacyAcousticSettingsChange> with
                member __.Change projection systemContext changeRequest =
                    change projection systemContext changeRequest

                member __.Constrain projection systemContext =
                    constrain projection systemContext

                member __.ToAcquisitionSettings projection systemContext =
                    toSettings projection systemContext
        }
