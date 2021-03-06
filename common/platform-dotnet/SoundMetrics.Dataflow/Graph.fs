﻿namespace SoundMetrics.Dataflow

open System
open System.Threading.Tasks
open System.Threading.Tasks.Dataflow

module Graph =

    // This has right associativity so, more or less, the graph binds from
    // right to left. Pointing the arrow right may be less correct in that
    // respect, but it is more readable when you're looking at a graph.
    let inline (^|>) f a = f a

    let inline fst (item, _, _) = item
    let inline snd (_, item, _) = item
    let inline thd (_, _, item) = item

    type RH<'T> = ITargetBlock<'T> * Task list * IDisposable list

    let dfbOptions = DataflowLinkOptions(PropagateCompletion = true)

    let leaf<'T> (f : 'T -> unit) =
        let block = ActionBlock<'T>(f)
        block :> ITargetBlock<'T>, [block.Completion], []

    let buffer<'T> boundedCapacity (rh : ITargetBlock<'T>, leaves, disposables) =

        let buffer =  if boundedCapacity > 0 then
                        let options = DataflowBlockOptions(BoundedCapacity = boundedCapacity)
                        BufferBlock<'T>(options)
                      else
                        BufferBlock<'T>()

        let d = buffer.LinkTo(rh, dfbOptions)
        buffer :> ITargetBlock<'T>, leaves, d :: disposables

    let private completeTargets (template : Task) (targets : ITargetBlock<_> seq) =

        // This logic is patterned after that in section 3 of
        // "Guide to Implementing Custom TPL Dataflow Blocks"
        // https://download.microsoft.com/download/1/6/1/1615555D-287C-4159-8491-8E5644C43CBA/Guide%20to%20Implementing%20Custom%20TPL%20Dataflow%20Blocks.pdf

        let complete : ITargetBlock<_> -> unit =
            if template.IsFaulted then
                fun target -> target.Fault(template.Exception)
            else
                fun target -> target.Complete()

        targets |> Seq.iter complete

    let tee<'T> (rhs : Tuple<ITargetBlock<'T>, Task list, IDisposable list> list) =

        let cached = rhs |> Seq.cache
        let targets : ITargetBlock<'T> seq = cached |> Seq.map fst |> Seq.cache
        let allLeaves = cached |> Seq.map snd |> List.concat
        let allDisposables : (IDisposable list) = cached |> Seq.map thd |> List.concat
        let action = ActionBlock<'T>(fun t ->
                        targets |> Seq.iter (fun target -> target.Post(t) |> ignore))

        action.Completion.ContinueWith(
                fun completion -> targets |> completeTargets completion
            )
            |> ignore

        action :> ITargetBlock<'T>, allLeaves, allDisposables

    let transform<'T,'U> (f : 'T -> 'U) (rhs : ITargetBlock<'U>, leaves, disposables) =

        let tf = TransformBlock<'T,'U>(f)
        let d = tf.LinkTo(rhs, dfbOptions)
        tf :> ITargetBlock<'T>, leaves, d :: disposables

    let filter<'T> (predicate : 'T -> bool) (target : ITargetBlock<'T>, leaves, disposables) =

        let action = ActionBlock<'T>(fun t ->
                        if predicate t then
                            target.Post(t) |> ignore)

        action.Completion.ContinueWith(
                fun completion -> target |> Seq.singleton |> completeTargets completion
            ) |> ignore

        action :> ITargetBlock<'T>, leaves, disposables

    open Serilog // No logging above this line, please.
    open System.Diagnostics

    type GraphHandle<'T> (name : string, root : RH<'T>) =

        let mutable disposed = false
        let mutable completing = false
        let root, leaves, disposables = root

        let completeAndWait (timeout : TimeSpan) =
            completing <- true
            root.Complete()
            Task.WaitAll(leaves |> Seq.toArray, timeout)

        let dispose disposing =

            let sw = Stopwatch.StartNew()

            if disposed then
                let msg = sprintf "An attempt was made to dispose an already-disposed GraphHandle '%s'"
                                  name
                let exn = (ObjectDisposedException ("GraphHandle", msg))
                Log.Error(exn, msg)
                raise exn

            if disposing then
                disposed <- true
                Log.Verbose("Disposing GraphHandle '{name}'", name)

                if not completing then
                    completeAndWait (TimeSpan.FromSeconds(1.0)) |> ignore

                // Clean up managed resources
                disposables |> List.iter (fun d -> d.Dispose())

                Log.Verbose("Disposing GraphHandle '{name}' duration: {duration}", sw.Elapsed)

            // Clean up unmanaged resources
            ()

        do Log.Verbose("Constructed GraphHandle '{name}'")

        interface IDisposable with
            member me.Dispose() = dispose true
                                  GC.SuppressFinalize(me)

        member me.Dispose() = (me :> IDisposable).Dispose()
        override __.Finalize() = dispose false

        member __.Post(t : 'T) : bool =

            if disposed then
                let msg = sprintf "An attempt was made to Post to an already-disposed GraphHandle '%s'"
                                  name
                let exn = (ObjectDisposedException ("GraphHandle", msg))
                Log.Error(exn, msg)
                raise exn

            if not completing then
                root.Post(t)
            else
                false

        member __.CompleteAndWait(timeout : TimeSpan) : bool =

            completeAndWait timeout
