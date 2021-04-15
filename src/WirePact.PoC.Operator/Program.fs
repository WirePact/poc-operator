namespace WirePact.PoC.Operator

open System
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open KubeOps.Operator
open WirePact.PoC.Operator.Envoy
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

module Program =
    let createHostBuilder args =
        Host
            .CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webBuilder -> webBuilder.UseStartup<Startup>() |> ignore)

    [<EntryPoint>]
    let main args =
        let b =
            EnvoyConfig.CreateSidecarConfig(80u, 5000u, 8080u)

        let serializer =
            SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()

        let res = serializer.Serialize b
        Console.WriteLine res

        (task {
            return!
                createHostBuilder(args)
                    .Build()
                    .RunOperatorAsync args
         })
            .Result
