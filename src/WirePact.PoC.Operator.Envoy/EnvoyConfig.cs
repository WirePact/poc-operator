using System;
using Envoy.Config.Bootstrap.V3;
using Envoy.Config.Cluster.V3;
using Envoy.Config.Endpoint.V3;
using Envoy.Extensions.Upstreams.Http.V3;
using Google.Protobuf.WellKnownTypes;

namespace WirePact.PoC.Operator.Envoy
{
    public static class EnvoyConfig
    {
        public static Bootstrap CreateSidecarConfig(uint targetPort, uint translatorPort, uint envoyPort) =>
            new()
            {
                StaticResources = new()
                {
                    Clusters =
                    {
                        new Cluster
                        {
                            Name = "target_service",
                            ConnectTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(30)),
                            Type = Cluster.Types.DiscoveryType.Static,
                            LoadAssignment = new()
                            {
                                ClusterName = "target_service",
                                Endpoints =
                                {
                                    new LocalityLbEndpoints
                                    {
                                        LbEndpoints =
                                        {
                                            new LbEndpoint
                                            {
                                                Endpoint = new()
                                                {
                                                    Address = new()
                                                    {
                                                        SocketAddress = new()
                                                        {
                                                            Address = "127.0.0.1",
                                                            PortValue = targetPort,
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                        new Cluster
                        {
                            Name = "auth_translator",
                            ConnectTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(0.25)),
                            Type = Cluster.Types.DiscoveryType.Static,
                            TypedExtensionProtocolOptions =
                            {
                                {
                                    "envoy.extensions.upstreams.http.v3.HttpProtocolOptions", Any.Pack(
                                        new HttpProtocolOptions
                                        {
                                            ExplicitHttpConfig = new() { Http2ProtocolOptions = new() },
                                        })
                                },
                            },
                            LoadAssignment = new()
                            {
                                ClusterName = "auth_translator",
                                Endpoints =
                                {
                                    new LocalityLbEndpoints
                                    {
                                        LbEndpoints =
                                        {
                                            new LbEndpoint
                                            {
                                                Endpoint = new()
                                                {
                                                    Address = new()
                                                    {
                                                        SocketAddress = new()
                                                        {
                                                            Address = "127.0.0.1",
                                                            PortValue = translatorPort,
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
    }
}
