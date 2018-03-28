﻿// Copyright 2014-2018 Sound Metrics Corp. All Rights Reserved.

namespace SoundMetrics.Aris.Comms

open Microsoft.FSharp.NativeInterop
open System
open System.Diagnostics
open System.Reactive.Subjects

// For native pointers:
// warning FS0009: Uses of this construct may result in the generation of unverifiable .NET IL code. This warning can be disabled using '--nowarn:9' or '#nowarn "9"'.
#nowarn "9"

type WorkType =
    | IncomingFrame of Frame
    | Command of ProcessingCommand
    | Quit

type WorkUnit = {
    enqueueTime: Stopwatch
    work: WorkType
}
with
    static member Frame frame = { work = IncomingFrame frame; enqueueTime = Stopwatch.StartNew () }
    static member Command cmd = { work = Command cmd; enqueueTime = Stopwatch.StartNew () }
    static member Quit = { work = WorkType.Quit; enqueueTime = Stopwatch.StartNew () }

type Histogram = { values: int[] }
with
    static member Create () = { values = Array.zeroCreate<int> 256 }

    /// Crass attempt to do something faster than using managed arrays (much).
    member internal s.CreateUpdater () =
        let h = Runtime.InteropServices.GCHandle.Alloc(s.values, Runtime.InteropServices.GCHandleType.Pinned)
        let dispose = fun () -> h.Free()
        let addr = h.AddrOfPinnedObject()
        let basePtr = NativePtr.ofNativeInt<int> addr
        let incr = fun value ->
                    let p = NativePtr.add basePtr value
                    let current = NativePtr.read p
                    NativePtr.write p (current + 1)
        incr, dispose

type ProcessedFrameType =
    | Frame of frame: Frame * histogram: Histogram * isRecording: bool
    | Command of ProcessingCommand
    | Quit

type ProcessedFrame = {
    enqueueTime: Stopwatch
    work: ProcessedFrameType
}
with
    static member Frame (workUnit: WorkUnit) frame histogram isRecording =
        { work = Frame (frame, histogram, isRecording); enqueueTime = workUnit.enqueueTime }

    static member Command (workUnit: WorkUnit) (cmd: ProcessingCommand) = { work = Command cmd; enqueueTime = workUnit.enqueueTime }
    static member Quit (workUnit: WorkUnit) = { work = ProcessedFrameType.Quit; enqueueTime = workUnit.enqueueTime }

module internal FrameProcessing =
    open Serilog

    let private logTimeToProcessFrame (milliseconds : int64) =
        Log.Verbose("Time to process frame: {milliseconds} ms", milliseconds)

    type FrameBuffer = {
        FrameIndex: FrameIndex
        BeamCount : uint32
        SampleCount: uint32
        PingsPerFrame: uint32
        SampleData: NativeBuffer
    }
    with
        static member FromFrame (f: Frame) =
            let cfg = SonarConfig.getPingModeConfig f.Header.PingMode
            {
                FrameIndex = int f.Header.FrameIndex
                BeamCount  = cfg.channelCount
                SampleCount = f.Header.SamplesPerBeam
                PingsPerFrame = cfg.pingsPerFrame
                SampleData = f.SampleData
            }

    let channelReverseMap = [| 10; 2; 14; 6; 8; 0; 12; 4;
                               11; 3; 15; 7; 9; 1; 13; 5 |]

    let buildChannelReverseMultiples beamsPerPing pingsPerFrame =
        channelReverseMap |> Seq.take beamsPerPing
                          |> Seq.mapi (fun n _value -> channelReverseMap.[n] * pingsPerFrame)
                          |> Seq.toArray

    /// Used in place of 'for x in 1 .. 4 .. 16 do' as the 'skip' flavor uses a slow enumerator;
    /// syntax: forSkip 1 4 16 (fun x -> ...)
    let inline forSkip start skip finish (f: ^t -> unit) =
        let mutable n: ^t = start
        while n <= finish do
            f n
            n <- n + skip

    let inline reorderSampleBuffer (fb : FrameBuffer)
                                   (histogram : Histogram)
                                   (source : nativeint, destination : nativeint) =

        let outbuf = destination |> NativePtr.ofNativeInt<byte>
        let inputW = source |> NativePtr.ofNativeInt<uint32>

        let beamsPerPing = int (fb.BeamCount / fb.PingsPerFrame)
        let channelReverseMultipledMap = buildChannelReverseMultiples beamsPerPing (int fb.PingsPerFrame)

        let sampleStride = fb.BeamCount
        let bytesReadPerPing = sampleStride / fb.PingsPerFrame

        let updateHisto, disposeHistoUpdater = histogram.CreateUpdater ()

        let sizeofUint = 4u
        let dwordsToReadPerPing = bytesReadPerPing / sizeofUint
        let totalDwordsPerPing = (fb.BeamCount * fb.SampleCount / fb.PingsPerFrame) / sizeofUint
        assert(totalDwordsPerPing = (totalDwordsPerPing / dwordsToReadPerPing) * dwordsToReadPerPing)
        let bytesWritten = ref 0u // ref cell because of lambda expression used with forSkip can't capture mutables
        let inputOffset = ref 0u // outside the loops for perfiness

        // Note that "for i = x to y do" does not allow for an unsigned range

        let mutable idxDwordPing0 = 0u
        for idxSample = 0 to int fb.SampleCount - 1 do
            let mutable idxLocalSample = idxDwordPing0
            for idxPing = 0 to int fb.PingsPerFrame - 1 do
                //for idxDword in idxLocalSample .. 4 .. idxLocalSample + dwordsToReadPerPing - 1 do // slow due to enumerator used
                forSkip idxLocalSample 4u (idxLocalSample + dwordsToReadPerPing - 1u) (fun idxDword ->
                    let composed = idxSample * int fb.BeamCount + idxPing

                    assert((dwordsToReadPerPing / 4u) * 4u = dwordsToReadPerPing)
                    inputOffset := 0u // ref cell because of lambda expression used with forSkip can't capture mutables
                    //for channel in 0 .. 4 .. beamsPerPing - 1 do // slow due to enumerator used
                    forSkip 0 4 (beamsPerPing - 1) (fun channel ->
                        //let p = NativePtr.add inputW (int (idxDword + !inputOffset)) // inputW[idxDword + inputOffset]
                        //let values1 = NativePtr.read<uint32> p
                        let values1 = NativePtr.get inputW (int (idxDword + !inputOffset))

                        let outIndex1 = channelReverseMultipledMap.[channel] + composed
                        let outIndex2 = channelReverseMultipledMap.[channel+1] + composed
                        let outIndex3 = channelReverseMultipledMap.[channel+2] + composed
                        let outIndex4 = channelReverseMultipledMap.[channel+3] + composed

                        let byte1 = byte (values1 &&& uint32 0xFF)
                        let byte2 = byte ((values1 >>> 8) &&& uint32 0xFF)
                        let byte3 = byte ((values1 >>> 16) &&& uint32 0xFF)
                        let byte4 = byte ((values1 >>> 24) &&& uint32 0xFF)
                        NativePtr.write<byte> (NativePtr.add outbuf outIndex1) byte1
                        NativePtr.write<byte> (NativePtr.add outbuf outIndex2) byte2
                        NativePtr.write<byte> (NativePtr.add outbuf outIndex3) byte3
                        NativePtr.write<byte> (NativePtr.add outbuf outIndex4) byte4

//                        histogram.Increment (int byte1)
//                        histogram.Increment (int byte2)
//                        histogram.Increment (int byte3)
//                        histogram.Increment (int byte4)
                        updateHisto (int byte1)
                        updateHisto (int byte2)
                        updateHisto (int byte3)
                        updateHisto (int byte4)

                        bytesWritten := !bytesWritten + 4u
                        inputOffset := !inputOffset + 1u
                        )
                    )

                idxLocalSample <- idxLocalSample + totalDwordsPerPing
                (* end: for idxDword in idxLocalSample .. 4 .. idxLocalSample + dwordsToReadPerPing - 1 do *)
            idxDwordPing0 <- idxDwordPing0 + dwordsToReadPerPing
            (* end: for idxPing in 0 .. fb.pingsPerFrame - 1 do *)

        disposeHistoUpdater ()

    let reorderData (fb : FrameBuffer) =
        let sw = Stopwatch.StartNew()
        let beamsPerPing = int (fb.BeamCount / fb.PingsPerFrame)
        let channelReverseMultipledMap = buildChannelReverseMultiples beamsPerPing (int fb.PingsPerFrame)

        let sampleStride = fb.BeamCount
        let bytesReadPerPing = sampleStride / fb.PingsPerFrame

        let histogram = Histogram.Create ()
        let reorderedSampleData =
            let reorder = reorderSampleBuffer fb histogram
            fb.SampleData |> NativeBuffer.transform reorder

        sw.Stop()
        //System.Diagnostics.Trace.TraceInformation(sprintf "#### F# reordering ~%09d ticks; %d bytes written" sw.ElapsedTicks !bytesWritten)

        reorderedSampleData, histogram

    let generateHistogram (fb : FrameBuffer) =
        let histogram = Histogram.Create ()
        let updateHisto, disposeHistoUpdater = histogram.CreateUpdater ()

        let buildHistogram (source : nativeint) =
            let bytes = source |> NativePtr.ofNativeInt<byte>
            for offset = 0 to int fb.SampleData.Length - 1 do
                let sample = NativePtr.get bytes offset
                updateHisto (int sample)

        try
            fb.SampleData |> NativeBuffer.iter buildHistogram
            histogram
        finally
            disposeHistoUpdater()

    let reorderDataNotInlineForTest fb histogram = reorderData fb

    let inline processFrameBuffer reorder fb =
        if reorder then
            reorderData fb
        else
            fb.SampleData, (generateHistogram fb)

    type ProcessPipelineState = {
        isRecording: bool
    }
    with
        static member Create () = { isRecording = false }

    let processPipeline (earlyFrameSpur: ISubject<ProcessedFrame>)
                        (state: ProcessPipelineState ref)
                        (work: WorkUnit) =

        let sw = Stopwatch.StartNew()
        let output = match work.work with
                        | IncomingFrame frame ->
                            let fb = FrameBuffer.FromFrame frame
                            let reorder = (frame.Header.ReorderedSamples = 0u)
                            let sampleData, histogram = processFrameBuffer reorder fb
                            let newF =
                                if reorder then
                                    let mutable hdr = frame.Header
                                    hdr.ReorderedSamples <- 1u
                                    { Header = hdr; SampleData = sampleData }
                                else
                                    frame

                            let usingOriginalSampleData = Object.ReferenceEquals(frame.SampleData, sampleData)
                            if not usingOriginalSampleData then
                                frame.SampleData.Dispose()

                            ProcessedFrame.Frame work newF histogram (!state).isRecording

                        | WorkType.Command cmd ->
                            state := {
                                isRecording = match cmd with
                                              | StartRecording _ -> true
                                              | StopRecording _ -> false
                                              | StopStartRecording _ -> true
                            }

                            ProcessedFrame.Command work cmd

                        | WorkType.Quit -> ProcessedFrame.Quit work

        logTimeToProcessFrame sw.ElapsedMilliseconds
        earlyFrameSpur.OnNext (output)
        output
