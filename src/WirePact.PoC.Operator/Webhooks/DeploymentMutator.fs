namespace WirePact.PoC.Operator.Webhooks

open System
open System.Linq
open KubeOps.Operator.Webhooks
open k8s.Models

type WirePactMutator() =
    let rnd = Random()
    let portLowerBound = 40000
    let portUpperBound = 60000
    let deploymentAnnotation = "ch.wirepact/port"

    let randomPort (excludeList: int list) =
        let mutable port = rnd.Next(portLowerBound, portUpperBound)

        while excludeList.Contains port do
            port <- rnd.Next(portLowerBound, portUpperBound)

        port

    interface IMutationWebhook<V1Deployment> with
        member this.Operations = AdmissionOperations.Create

        member this.Create(newEntity, _) =
            if newEntity.Annotations() = null
               || not
                  <| newEntity
                      .Annotations()
                      .ContainsKey deploymentAnnotation then
                MutationResult.NoChanges()
            else
                let targetPort =
                    newEntity.Annotations().Item deploymentAnnotation

                let usedPorts =
                    newEntity.Spec.Template.Spec.Containers
                    |> Seq.collect (fun c -> c.Ports)
                    |> Seq.map (fun p -> p.ContainerPort)
                    |> List.ofSeq

                let envoyPort = randomPort usedPorts
                let translatorPort = randomPort (envoyPort :: usedPorts)



                MutationResult.NoChanges()

//    interface IMutationWebhook<V1Secret> with
//        member this.Operations = AdmissionOperations.Create
//
//        member this.Create(newEntity, _) =
//            newEntity.Spec.Username <- "not foobar"
//            MutationResult.Modified(newEntity)
