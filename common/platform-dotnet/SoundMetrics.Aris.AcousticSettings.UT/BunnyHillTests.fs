﻿namespace SoundMetrics.Aris.AcousticSettings

open SoundMetrics.Aris.AcousticSettings.Experimental
open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols
open Microsoft.VisualStudio.TestTools.UnitTesting
open System

open BunnyHill

[<TestClass>]
type BunnyHillTests () =

    [<TestMethod>]
    member __.``Show constraint from float32 MIN/MAX`` () =
        let bh = {
            DownrangeWindow = { Start = 3.0<m>; End = Double.MaxValue * 1.0<m> } }
        let ctx = {
            SystemType = ArisSystemType.Aris3000
            WaterTemp = 20.0<degC>
            Salinity = Salinity.Seawater
            Depth = 10.0<m>
            AuxLens = AuxLensType.None
            AntialiasingPeriod = 0<Us>
        }

        let constrained = bunnyHillProjection.Constrain bh ctx

        Assert.AreNotEqual<BunnyHillSettings>(bh, constrained, "Must show *some* constraint!")

    [<TestMethod>]
    member __.``Change window start`` () =

        let initialStart, initialEnd = 3.0<m>, 5.0<m>
        let bh = { DownrangeWindow = { Start = initialStart; End = initialEnd } }
        let newWindowStart = initialStart + 0.1<m>
        let expectedWindow = { bh.DownrangeWindow with Start = newWindowStart }
        let change = ChangeWindowStart newWindowStart
        let systemContext = {
            SystemType = ArisSystemType.Aris3000
            WaterTemp = 20.0<degC>
            Salinity = Salinity.Seawater
            Depth = 10.0<m>
            AuxLens = AuxLensType.None
            AntialiasingPeriod = 0<Us>
        }

        let changed = bunnyHillProjection.Change bh systemContext change

        let actualWindow = changed.DownrangeWindow
        Assert.AreEqual<DownrangeWindow>(expectedWindow, actualWindow)

    [<TestMethod>]
    member __.``Change window end`` () =

        let initialStart, initialEnd = 3.0<m>, 5.0<m>
        let bh = { DownrangeWindow = { Start = initialStart; End = initialEnd } }
        let newWindowEnd = initialEnd + 0.1<m>
        let expectedWindow = { bh.DownrangeWindow with End = newWindowEnd }
        let change = ChangeWindowEnd newWindowEnd

        let systemContext = {
            SystemType = ArisSystemType.Aris3000
            WaterTemp = 20.0<degC>
            Salinity = Salinity.Seawater
            Depth = 10.0<m>
            AuxLens = AuxLensType.None
            AntialiasingPeriod = 0<Us>
        }

        let changed = bunnyHillProjection.Change bh systemContext change

        let actualWindow = changed.DownrangeWindow
        Assert.AreEqual<DownrangeWindow>(expectedWindow, actualWindow)

    [<TestMethod>]
    member __.``To settings`` () =

        let bh = { DownrangeWindow = { Start = 1.0<m>; End = 10.0<m> } }

        let systemContext = {
            SystemType = ArisSystemType.Aris3000
            WaterTemp = 20.0<degC>
            Salinity = Salinity.Seawater
            Depth = 10.0<m>
            AuxLens = AuxLensType.None
            AntialiasingPeriod = 0<Us>
        }

        let actual = bunnyHillProjection.ToAcquisitionSettings bh systemContext

        Assert.IsNotNull(actual)
